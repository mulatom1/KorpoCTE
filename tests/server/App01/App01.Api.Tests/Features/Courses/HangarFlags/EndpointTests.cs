using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

using App01.Modules.Courses.Features.HangarFlags;
using App01.Shared.Application.Entities.Courses;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Courses.HangarFlags;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string JwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";
    private const string XToken = "test-x-token";
    private const string Url = "/api/courses/hangar-flags";
    private const long CurrentUserId = 2;
    private const long OtherUserId = 3;
    private const string SecretCriteria = "TAJNE-KRYTERIUM-XYZ";

    // Stała chwila "teraz" dla testów
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    public EndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTime utcNow)
        {
            _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private WebApplicationFactory<Program> CreateFactory(Action<AppDbContext>? seedData = null)
    {
        // Stała nazwa bazy na fabrykę - seed i żądania widzą te same dane
        var dbName = $"TestDb_{Guid.NewGuid()}";

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = JwtKey,
                    ["Jwt:Issuer"] = JwtIssuer,
                    ["Jwt:Audience"] = JwtAudience,
                    ["Jwt:ExpiryInMinutes"] = "60",
                    ["Swagger:Enabled"] = "false",
                    ["Tokens:X-TOKEN"] = XToken,
                    ["ConnectionStrings:DefaultConnection"] = "Server=dummy;Database=dummy;Integrated Security=True;"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Usunięcie workerów w tle, żeby nie uruchamiały się podczas testów
                var hostedServicesToRemove = services
                    .Where(d => d.ServiceType == typeof(IHostedService))
                    .ToList();
                foreach (var descriptor in hostedServicesToRemove)
                {
                    services.Remove(descriptor);
                }

                var descriptorsToRemove = services
                    .Where(d => d.ServiceType.FullName != null &&
                               (d.ServiceType.FullName.Contains("DbContext") ||
                                d.ServiceType.FullName.Contains("EntityFramework")))
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName);
                });

                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));

                if (seedData != null)
                {
                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.Database.EnsureCreated();
                    seedData(db);
                    db.SaveChanges();
                }
            });
        });
    }

    private static string GenerateJwtToken(long userId, string email, bool isAdmin)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("isAdmin", isAdmin.ToString().ToLower()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, bool withXToken = true, bool withJwt = true)
    {
        var client = factory.CreateClient();
        if (withXToken)
        {
            client.DefaultRequestHeaders.Add("X-TOKEN", XToken);
        }
        if (withJwt)
        {
            var token = GenerateJwtToken(CurrentUserId, "user1@example.com", isAdmin: false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    // Kursy: 1 (najstarszy), 4 (nowszy), 2 (równo "teraz" - widoczny), 3 (nieopublikowany).
    // Bieżący użytkownik zdobył flagi 10 i 14 (remis dat), 16 oraz 15 (kurs nieopublikowany).
    // Flagę 11 zdobył tylko inny użytkownik. Flaga 13 nie ma kryteriów.
    private static void SeedFlags(AppDbContext db)
    {
        db.Users.AddRange(
            new User { Id = CurrentUserId, Email = "user1@example.com", CreatedAt = Now },
            new User { Id = OtherUserId, Email = "user2@example.com", CreatedAt = Now });

        db.Courses.AddRange(
            new Course { Id = 1, Slug = "kurs-starszy", PublishDate = Now.AddDays(-10) },
            new Course { Id = 4, Slug = "kurs-sredni", PublishDate = Now.AddDays(-5) },
            new Course { Id = 2, Slug = "kurs-nowszy", PublishDate = Now },
            new Course { Id = 3, Slug = "kurs-przyszly", PublishDate = Now.AddMinutes(1) });

        db.Flags.AddRange(
            new Flag { Id = 12, CourseId = 2, Code = "KOD-NOWSZY-12", Title = "Flaga nowsza", Criteria = SecretCriteria },
            new Flag { Id = 11, CourseId = 1, Code = "KOD-STARSZY-11", Title = "Flaga innego", Criteria = SecretCriteria },
            new Flag { Id = 10, CourseId = 1, Code = "KOD-STARSZY-10", Title = "Flaga zdobyta", Criteria = SecretCriteria },
            new Flag { Id = 13, CourseId = 1, Code = "KOD-STARSZY-13", Title = "Bez kryteriów", Criteria = null },
            new Flag { Id = 14, CourseId = 4, Code = "KOD-SREDNI-14", Title = "Remis daty", Criteria = SecretCriteria },
            new Flag { Id = 16, CourseId = 4, Code = "KOD-SREDNI-16", Title = "Zdobyta dawniej", Criteria = SecretCriteria },
            new Flag { Id = 17, CourseId = 4, Code = "KOD-SREDNI-17", Title = "Niezdobyta średnia", Criteria = SecretCriteria },
            new Flag { Id = 15, CourseId = 3, Code = "KOD-PRZYSZLY-15", Title = "Flaga przyszła", Criteria = SecretCriteria });

        // Id 1 i 4 z Kind = Unspecified - tak datetime2 czyta SQL Server; test UTC sprawdza, że handler dokleja Z
        db.UserFlags.AddRange(
            new UserFlag { Id = 1, UserId = CurrentUserId, FlagId = 10, EarnedAt = DateTime.SpecifyKind(Now.AddDays(-1), DateTimeKind.Unspecified) },
            new UserFlag { Id = 2, UserId = OtherUserId, FlagId = 11, EarnedAt = Now.AddDays(-1) },
            new UserFlag { Id = 3, UserId = CurrentUserId, FlagId = 14, EarnedAt = Now.AddDays(-1) },
            new UserFlag { Id = 4, UserId = CurrentUserId, FlagId = 16, EarnedAt = DateTime.SpecifyKind(Now.AddDays(-3), DateTimeKind.Unspecified) },
            new UserFlag { Id = 5, UserId = CurrentUserId, FlagId = 15, EarnedAt = Now.AddHours(-1) });
    }

    private static async Task<Contracts.Response> GetFlagsAsync(HttpClient client, string query = "")
    {
        var response = await client.GetAsync(Url + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Fallback SPA też zwraca 200 - sprawdzamy, że to JSON z kontraktu
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        return result;
    }

    [Fact]
    public async Task HangarFlags_ReturnsEarnedOnlyForCurrentUser()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedFlags));

        // Act
        var result = await GetFlagsAsync(client);

        // Assert
        var own = Assert.Single(result.Flags, f => f.FlagId == 10);
        Assert.True(own.IsEarned);
        Assert.Equal(Now.AddDays(-1), own.EarnedAt);
        Assert.Equal("KOD-STARSZY-10", own.Code);

        var others = Assert.Single(result.Flags, f => f.FlagId == 11);
        Assert.False(others.IsEarned);
        Assert.Null(others.EarnedAt);
        Assert.Null(others.Code);
    }

    [Fact]
    public async Task HangarFlags_IncludesFlagsWithoutCriteria()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedFlags));

        // Act
        var result = await GetFlagsAsync(client);

        // Assert
        var flag = Assert.Single(result.Flags, f => f.FlagId == 13);
        Assert.Equal(new Contracts.HangarFlagDto(13, "Bez kryteriów", "kurs-starszy", false, null, null), flag);
    }

    [Fact]
    public async Task HangarFlags_SkipsUnpublishedCourses()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedFlags));

        // Act
        var result = await GetFlagsAsync(client);

        // Assert
        // Flaga 15 jest zdobyta, ale jej kurs nie jest jeszcze opublikowany
        Assert.DoesNotContain(result.Flags, f => f.FlagId == 15);
        Assert.DoesNotContain(result.Flags, f => f.CourseSlug == "kurs-przyszly");
        // Kurs z PublishDate == teraz jest widoczny
        Assert.Contains(result.Flags, f => f.FlagId == 12 && f.CourseSlug == "kurs-nowszy");
    }

    [Fact]
    public async Task HangarFlags_OrdersEarnedByEarnedAtDescThenUnearnedByPublishDate()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedFlags));

        // Act
        var result = await GetFlagsAsync(client);

        // Assert
        // Zdobyte: 10 i 14 (remis daty - rosnąco po Id), potem 16;
        // niezdobyte: kurs 1 (11, 13), kurs 4 (17), kurs 2 (12)
        Assert.Equal([10, 14, 16, 11, 13, 17, 12], result.Flags.Select(f => f.FlagId));
    }

    [Fact]
    public async Task HangarFlags_EarnedAtIsUtc()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedFlags));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"earnedAt\":\"2026-05-31T12:00:00Z\"", json);
        Assert.Contains("\"earnedAt\":\"2026-05-29T12:00:00Z\"", json);
    }

    [Fact]
    public async Task HangarFlags_ResponseDoesNotContainCriteria()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedFlags));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("criteria", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SecretCriteria, json);
    }

    [Fact]
    public async Task HangarFlags_ReturnsCodeOnlyForFlagsEarnedByCurrentUser()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedFlags));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        // Kody zdobytych flag bieżącego użytkownika są w odpowiedzi
        Assert.Contains("\"code\":\"KOD-STARSZY-10\"", json);
        Assert.Contains("\"code\":\"KOD-SREDNI-14\"", json);
        Assert.Contains("\"code\":\"KOD-SREDNI-16\"", json);
        // Kody niezdobytych (także zdobytej tylko przez innego użytkownika) nigdy nie wychodzą z serwera
        Assert.DoesNotContain("KOD-STARSZY-11", json);
        Assert.DoesNotContain("KOD-STARSZY-13", json);
        Assert.DoesNotContain("KOD-SREDNI-17", json);
        Assert.DoesNotContain("KOD-NOWSZY-12", json);
        Assert.DoesNotContain("KOD-PRZYSZLY-15", json);
    }

    [Fact]
    public async Task HangarFlags_WithoutQuery_UsesDefaultsAllFirstPageOf20()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedFlags));

        // Act
        var result = await GetFlagsAsync(client);

        // Assert
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(7, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(7, result.AllCount);
        Assert.Equal(3, result.EarnedCount);
    }

    [Fact]
    public async Task HangarFlags_FilterEarned_ReturnsOnlyEarnedAndCountersIgnoreFilter()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedFlags));

        // Act
        var result = await GetFlagsAsync(client, "?filter=Earned");

        // Assert
        Assert.Equal([10, 14, 16], result.Flags.Select(f => f.FlagId));
        Assert.Equal(3, result.TotalCount);
        // Licznik hangaru liczy wszystkie widoczne flagi, niezależnie od filtra
        Assert.Equal(7, result.AllCount);
        Assert.Equal(3, result.EarnedCount);
    }

    [Fact]
    public async Task HangarFlags_FilterUnearned_ReturnsOnlyUnearned()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedFlags));

        // Act
        var result = await GetFlagsAsync(client, "?filter=Unearned");

        // Assert
        Assert.Equal([11, 13, 17, 12], result.Flags.Select(f => f.FlagId));
        Assert.Equal(4, result.TotalCount);
        Assert.All(result.Flags, f => Assert.Null(f.Code));
    }

    [Theory]
    [InlineData(1, new[] { 10, 14, 16 })]
    [InlineData(2, new[] { 11, 13, 17 })]
    [InlineData(3, new[] { 12 })]
    [InlineData(4, new int[0])]
    public async Task HangarFlags_Paginates_KeepingSortOrderAcrossPages(int page, int[] expectedIds)
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedFlags));

        // Act
        var result = await GetFlagsAsync(client, $"?page={page}&pageSize=3");

        // Assert
        Assert.Equal(expectedIds, result.Flags.Select(f => f.FlagId));
        Assert.Equal(page, result.Page);
        Assert.Equal(3, result.PageSize);
        Assert.Equal(7, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Theory]
    [InlineData("?filter=Foo")]
    [InlineData("?filter=earned")]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    public async Task HangarFlags_WithInvalidQuery_ReturnsBadRequest(string query)
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedFlags));

        // Act
        var response = await client.GetAsync(Url + query);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HangarFlags_WithoutFlags_ReturnsEmptyList()
    {
        // Arrange
        var client = CreateClient(CreateFactory());

        // Act
        var result = await GetFlagsAsync(client);

        // Assert
        Assert.Empty(result.Flags);
    }

    [Fact]
    public async Task HangarFlags_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedFlags), withJwt: false);

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HangarFlags_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedFlags), withXToken: false);

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}