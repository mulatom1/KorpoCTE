using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

using App01.Modules.Courses.Features.ActivateFlag;
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


namespace App01.Bootstrapper.Api.Tests.Features.Courses.ActivateFlag;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string JwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";
    private const string XToken = "test-x-token";
    private const string Url = "/api/courses/activate-flag";
    private const long CurrentUserId = 2;
    private const long OtherUserId = 3;
    private const string SecretCriteria = "TAJNE-KRYTERIUM-XYZ";

    private const int PublishedFlagId = 10;
    private const int OwnedFlagId = 11;
    private const int NoCriteriaFlagId = 13;
    private const int PublishedNowFlagId = 14;
    private const int UnpublishedFlagId = 15;

    private const string PublishedCode = "KOD-ABC-1";
    private const string OwnedCode = "KOD-OWNED-2";
    private const string NoCriteriaCode = "KOD-BEZ-KRYTERIOW-3";
    private const string PublishedNowCode = "KOD-TERAZ-4";
    private const string UnpublishedCode = "KOD-PRZYSZLY-5";

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

    private WebApplicationFactory<Program> CreateFactory()
    {
        // Stała nazwa bazy na fabrykę - seed, żądania i asercje widzą te same dane
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

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
                SeedData(db);
                db.SaveChanges();
            });
        });
    }

    // Kurs opublikowany (1), opublikowany równo "teraz" (2) i nieopublikowany (3).
    // Flaga 10 do aktywacji (zdobyta tylko przez innego użytkownika), flaga 11 zdobyta już przez bieżącego,
    // flaga 13 bez kryteriów, flaga 14 w kursie z PublishDate == teraz, flaga 15 w kursie nieopublikowanym.
    private static void SeedData(AppDbContext db)
    {
        db.Users.AddRange(
            new User { Id = CurrentUserId, Email = "user1@example.com", CreatedAt = Now },
            new User { Id = OtherUserId, Email = "user2@example.com", CreatedAt = Now });

        db.Courses.AddRange(
            new Course { Id = 1, Slug = "kurs-opublikowany", PublishDate = Now.AddDays(-10) },
            new Course { Id = 2, Slug = "kurs-teraz", PublishDate = Now },
            new Course { Id = 3, Slug = "kurs-przyszly", PublishDate = Now.AddMinutes(1) });

        db.Flags.AddRange(
            new Flag { Id = PublishedFlagId, CourseId = 1, Code = PublishedCode, Title = "Zadanie 1", Criteria = SecretCriteria },
            new Flag { Id = OwnedFlagId, CourseId = 1, Code = OwnedCode, Title = "Zadanie 2", Criteria = SecretCriteria },
            new Flag { Id = NoCriteriaFlagId, CourseId = 1, Code = NoCriteriaCode, Title = "Bez kryteriów", Criteria = null },
            new Flag { Id = PublishedNowFlagId, CourseId = 2, Code = PublishedNowCode, Title = "Zadanie teraz", Criteria = SecretCriteria },
            new Flag { Id = UnpublishedFlagId, CourseId = 3, Code = UnpublishedCode, Title = "Zadanie przyszłe", Criteria = SecretCriteria });

        db.UserFlags.AddRange(
            new UserFlag { Id = 1, UserId = CurrentUserId, FlagId = OwnedFlagId, EarnedAt = Now.AddDays(-1) },
            new UserFlag { Id = 2, UserId = OtherUserId, FlagId = PublishedFlagId, EarnedAt = Now.AddDays(-1) });
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

    private static List<UserFlag> GetCurrentUserFlags(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.UserFlags.Where(uf => uf.UserId == CurrentUserId).ToList();
    }

    private static async Task<Contracts.Response> PostAndReadOk(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync(Url, new Contracts.Request(code));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Fallback SPA też zwraca 200 - sprawdzamy, że to JSON z kontraktu
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        return result;
    }

    // ===== Activated =====

    [Fact]
    public async Task ActivateFlag_WithValidCode_SavesUserFlagAndReturnsActivated()
    {
        // Arrange
        var factory = CreateFactory();
        var client = CreateClient(factory);

        // Act
        var result = await PostAndReadOk(client, PublishedCode);

        // Assert
        Assert.Equal(Contracts.Statuses.Activated, result.Status);
        Assert.Equal("Flaga aktywowana!", result.Message);
        Assert.Equal("Zadanie 1", result.FlagTitle);
        var saved = Assert.Single(GetCurrentUserFlags(factory), uf => uf.FlagId == PublishedFlagId);
        Assert.Equal(Now, saved.EarnedAt);
    }

    [Fact]
    public async Task ActivateFlag_WithTrimmedLowercaseCode_Activates()
    {
        // Arrange
        var factory = CreateFactory();
        var client = CreateClient(factory);

        // Act
        var result = await PostAndReadOk(client, " kod-abc-1 ");

        // Assert
        Assert.Equal(Contracts.Statuses.Activated, result.Status);
        Assert.Single(GetCurrentUserFlags(factory), uf => uf.FlagId == PublishedFlagId);
    }

    [Fact]
    public async Task ActivateFlag_FlagWithoutCriteria_Activates()
    {
        // Arrange
        var factory = CreateFactory();
        var client = CreateClient(factory);

        // Act
        var result = await PostAndReadOk(client, NoCriteriaCode);

        // Assert
        Assert.Equal(Contracts.Statuses.Activated, result.Status);
        Assert.Equal("Bez kryteriów", result.FlagTitle);
        Assert.Single(GetCurrentUserFlags(factory), uf => uf.FlagId == NoCriteriaFlagId);
    }

    // ===== AlreadyOwned =====

    [Fact]
    public async Task ActivateFlag_AlreadyOwned_ReturnsAlreadyOwnedWithoutSecondRow()
    {
        // Arrange
        var factory = CreateFactory();
        var client = CreateClient(factory);

        // Act
        var result = await PostAndReadOk(client, OwnedCode);

        // Assert
        Assert.Equal(Contracts.Statuses.AlreadyOwned, result.Status);
        Assert.Equal("Masz już tę flagę.", result.Message);
        Assert.Equal("Zadanie 2", result.FlagTitle);
        var owned = Assert.Single(GetCurrentUserFlags(factory), uf => uf.FlagId == OwnedFlagId);
        Assert.Equal(Now.AddDays(-1), owned.EarnedAt);
    }

    [Fact]
    public async Task ActivateFlag_OwnedByOtherUser_ActivatesForCurrentUser()
    {
        // Arrange - flagę 10 zdobył już inny użytkownik
        var factory = CreateFactory();
        var client = CreateClient(factory);

        // Act
        var result = await PostAndReadOk(client, PublishedCode);

        // Assert
        Assert.Equal(Contracts.Statuses.Activated, result.Status);
        Assert.Single(GetCurrentUserFlags(factory), uf => uf.FlagId == PublishedFlagId);
    }

    // ===== Invalid =====

    [Fact]
    public async Task ActivateFlag_UnknownCode_ReturnsInvalid()
    {
        // Arrange
        var factory = CreateFactory();
        var client = CreateClient(factory);
        var before = GetCurrentUserFlags(factory).Count;

        // Act
        var result = await PostAndReadOk(client, "KOD-NIEISTNIEJACY");

        // Assert
        Assert.Equal(Contracts.Statuses.Invalid, result.Status);
        Assert.Equal("Nieprawidłowy kod flagi.", result.Message);
        Assert.Null(result.FlagTitle);
        Assert.Equal(before, GetCurrentUserFlags(factory).Count);
    }

    [Fact]
    public async Task ActivateFlag_UnpublishedCourseCode_ReturnsInvalid()
    {
        // Arrange
        var factory = CreateFactory();
        var client = CreateClient(factory);

        // Act
        var unpublished = await PostAndReadOk(client, UnpublishedCode);
        var publishedNow = await PostAndReadOk(client, PublishedNowCode);

        // Assert - kurs przyszły nie aktywuje, kurs z PublishDate == teraz aktywuje
        Assert.Equal(Contracts.Statuses.Invalid, unpublished.Status);
        Assert.Null(unpublished.FlagTitle);
        Assert.DoesNotContain(GetCurrentUserFlags(factory), uf => uf.FlagId == UnpublishedFlagId);

        Assert.Equal(Contracts.Statuses.Activated, publishedNow.Status);
        Assert.Single(GetCurrentUserFlags(factory), uf => uf.FlagId == PublishedNowFlagId);
    }

    [Fact]
    public async Task ActivateFlag_ResponseDoesNotContainCode()
    {
        // Arrange
        var client = CreateClient(CreateFactory());

        // Act
        var response = await client.PostAsJsonAsync(Url, new Contracts.Request(PublishedCode));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Activated", json);
        Assert.DoesNotContain(PublishedCode, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SecretCriteria, json);
    }

    // ===== 400 =====

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task ActivateFlag_WithEmptyOrTooLongCode_ReturnsBadRequest(string code)
    {
        // Arrange
        var factory = CreateFactory();
        var client = CreateClient(factory);
        var before = GetCurrentUserFlags(factory).Count;

        // Act
        var response = await client.PostAsJsonAsync(Url, new Contracts.Request(code));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, GetCurrentUserFlags(factory).Count);
    }

    // ===== 401 / X-TOKEN =====

    [Fact]
    public async Task ActivateFlag_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateClient(CreateFactory(), withJwt: false);

        // Act
        var response = await client.PostAsJsonAsync(Url, new Contracts.Request(PublishedCode));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ActivateFlag_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateClient(CreateFactory(), withXToken: false);

        // Act
        var response = await client.PostAsJsonAsync(Url, new Contracts.Request(PublishedCode));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}