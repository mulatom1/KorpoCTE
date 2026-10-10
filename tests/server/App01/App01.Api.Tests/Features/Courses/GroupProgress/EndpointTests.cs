using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using App01.Modules.Courses.Features.GroupProgress;
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


namespace App01.Bootstrapper.Api.Tests.Features.Courses.GroupProgress;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string JwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";
    private const string XToken = "test-x-token";
    private const string Url = "/api/courses/group-progress";
    private const long CurrentUserId = 2;

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

    // Kurs z flagami o kolejnych Id od firstFlagId
    private static void AddCourse(AppDbContext db, int courseId, DateTime publishDate, int firstFlagId, int flagCount)
    {
        db.Courses.Add(new Course { Id = courseId, Slug = $"kurs-{courseId}", PublishDate = publishDate });
        for (var i = 0; i < flagCount; i++)
        {
            var flagId = firstFlagId + i;
            db.Flags.Add(new Flag { Id = flagId, CourseId = courseId, Code = $"KOD-{flagId}", Title = $"Flaga {flagId}" });
        }
    }

    private static void AddUser(AppDbContext db, long id, string email, bool isAdmin = false)
    {
        db.Users.Add(new User { Id = id, Email = email, IsAdmin = isAdmin, CreatedAt = Now });
    }

    private static void AddUserFlag(AppDbContext db, long userId, int flagId, DateTime earnedAt)
    {
        db.UserFlags.Add(new UserFlag { UserId = userId, FlagId = flagId, EarnedAt = earnedAt });
    }

    private static string AsOfQuery(DateTime asOf)
    {
        return "?asOf=" + Uri.EscapeDataString(asOf.ToString("O"));
    }

    private static async Task<Contracts.Response> GetGroupProgressAsync(HttpClient client, string query = "")
    {
        var response = await client.GetAsync(Url + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Fallback SPA też zwraca 200 - sprawdzamy, że to JSON z kontraktu
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        return result;
    }

    private static async Task<string> GetRawAsOfAsync(HttpClient client, string query = "")
    {
        var response = await client.GetAsync(Url + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var asOf = json.RootElement.GetProperty("asOf").GetString();
        Assert.NotNull(asOf);
        return asOf;
    }

    [Fact]
    public async Task GroupProgress_WithoutAsOf_ComputesForNow()
    {
        // Arrange
        // Przykład z planu: 2 osoby, 10 flag, obie zdobyły A, jedna także B
        var client = CreateClient(CreateFactory(db =>
        {
            AddCourse(db, 1, Now.AddDays(-30), firstFlagId: 1, flagCount: 10);
            AddUser(db, 2, "anna@example.com");
            AddUser(db, 3, "jan@example.com");
            AddUserFlag(db, 2, 1, Now.AddDays(-5));
            AddUserFlag(db, 3, 1, Now.AddDays(-4));
            AddUserFlag(db, 3, 2, Now.AddDays(-3));
        }));

        // Act
        var result = await GetGroupProgressAsync(client);

        // Assert
        Assert.Equal(Now, result.AsOf);
        Assert.Equal(2, result.UserCount);
        Assert.Equal(10, result.AvailableFlagCount);
        Assert.Equal(2, result.EarnedFlagCount);
        Assert.Equal(20.0, result.EarnedPercent);
    }

    [Fact]
    public async Task GroupProgress_SharedFlagCountedOnce()
    {
        // Arrange
        var client = CreateClient(CreateFactory(db =>
        {
            AddCourse(db, 1, Now.AddDays(-30), firstFlagId: 1, flagCount: 3);
            AddUser(db, 2, "a@example.com");
            AddUser(db, 3, "b@example.com");
            AddUser(db, 4, "c@example.com");
            AddUserFlag(db, 2, 1, Now.AddDays(-3));
            AddUserFlag(db, 3, 1, Now.AddDays(-2));
            AddUserFlag(db, 4, 1, Now.AddDays(-1));
        }));

        // Act
        var result = await GetGroupProgressAsync(client);

        // Assert
        Assert.Equal(3, result.UserCount);
        Assert.Equal(3, result.AvailableFlagCount);
        Assert.Equal(1, result.EarnedFlagCount);
        // 1 / 3 = 33,33% - zaokrąglenie do 1 miejsca po przecinku
        Assert.Equal(33.3, result.EarnedPercent);
    }

    [Fact]
    public async Task GroupProgress_IncludesAdmins()
    {
        // Arrange
        var client = CreateClient(CreateFactory(db =>
        {
            AddCourse(db, 1, Now.AddDays(-30), firstFlagId: 1, flagCount: 4);
            AddUser(db, 2, "uczestnik@example.com");
            AddUser(db, 5, "admin@example.com", isAdmin: true);
            AddUserFlag(db, 2, 1, Now.AddDays(-2));
            AddUserFlag(db, 5, 2, Now.AddDays(-1));
        }));

        // Act
        var result = await GetGroupProgressAsync(client);

        // Assert
        Assert.Equal(2, result.UserCount);
        Assert.Equal(2, result.EarnedFlagCount);
        Assert.Equal(50.0, result.EarnedPercent);
    }

    [Fact]
    public async Task GroupProgress_SkipsUnpublishedCourses()
    {
        // Arrange
        // Kurs 1 opublikowany; kurs 2 z PublishDate == teraz (granica ostra - nieliczony); kurs 3 przyszły
        var client = CreateClient(CreateFactory(db =>
        {
            AddCourse(db, 1, Now.AddDays(-30), firstFlagId: 1, flagCount: 2);
            AddCourse(db, 2, Now, firstFlagId: 20, flagCount: 3);
            AddCourse(db, 3, Now.AddDays(1), firstFlagId: 30, flagCount: 4);
            AddUser(db, 2, "a@example.com");
            AddUser(db, 3, "b@example.com");
            AddUser(db, 4, "c@example.com");
            AddUserFlag(db, 2, 1, Now.AddDays(-1));
            // Zdobycia flag kursów nieopublikowanych przed asOf nie są liczone
            AddUserFlag(db, 3, 20, Now.AddHours(-1));
            AddUserFlag(db, 4, 30, Now.AddHours(-1));
        }));

        // Act
        var result = await GetGroupProgressAsync(client);

        // Assert
        Assert.Equal(1, result.UserCount);
        Assert.Equal(2, result.AvailableFlagCount);
        Assert.Equal(1, result.EarnedFlagCount);
        Assert.Equal(50.0, result.EarnedPercent);
    }

    [Fact]
    public async Task GroupProgress_AsOfInPast_CountsOnlyEarlierEvents()
    {
        // Arrange
        var asOf = Now.AddDays(-2);
        var client = CreateClient(CreateFactory(db =>
        {
            AddCourse(db, 1, Now.AddDays(-30), firstFlagId: 1, flagCount: 4);
            // Kurs opublikowany dokładnie w asOf - jeszcze niedostępny
            AddCourse(db, 2, asOf, firstFlagId: 20, flagCount: 6);
            AddUser(db, 2, "a@example.com");
            AddUser(db, 3, "b@example.com");
            AddUser(db, 4, "c@example.com");
            // Zdobycie dokładnie w asOf nie jest liczone
            AddUserFlag(db, 2, 1, asOf);
            // Zdobycie 1 tick przed asOf jest liczone
            AddUserFlag(db, 3, 2, asOf.AddTicks(-1));
            // Zdobycie po asOf nie jest liczone
            AddUserFlag(db, 4, 3, Now.AddDays(-1));
        }));

        // Act
        var result = await GetGroupProgressAsync(client, AsOfQuery(asOf));

        // Assert
        Assert.Equal(asOf, result.AsOf);
        Assert.Equal(1, result.UserCount);
        Assert.Equal(4, result.AvailableFlagCount);
        Assert.Equal(1, result.EarnedFlagCount);
        Assert.Equal(25.0, result.EarnedPercent);
    }

    [Fact]
    public async Task GroupProgress_AsOfInFuture_IsClampedToNow()
    {
        // Arrange
        var client = CreateClient(CreateFactory(db =>
        {
            AddCourse(db, 1, Now.AddDays(-30), firstFlagId: 1, flagCount: 2);
            // Kurs opublikowany jutro - nie może być ujawniony przez przyszłe asOf
            AddCourse(db, 2, Now.AddDays(1), firstFlagId: 20, flagCount: 8);
            AddUser(db, 2, "a@example.com");
            AddUserFlag(db, 2, 1, Now.AddDays(-1));
        }));

        // Act
        var result = await GetGroupProgressAsync(client, AsOfQuery(Now.AddDays(10)));
        var rawAsOf = await GetRawAsOfAsync(client, AsOfQuery(Now.AddDays(10)));

        // Assert
        Assert.Equal(Now, result.AsOf);
        Assert.Equal("2026-06-01T12:00:00Z", rawAsOf);
        Assert.Equal(1, result.UserCount);
        Assert.Equal(2, result.AvailableFlagCount);
        Assert.Equal(1, result.EarnedFlagCount);
        Assert.Equal(50.0, result.EarnedPercent);
    }

    [Fact]
    public async Task GroupProgress_NoFlags_ReturnsNullPercent()
    {
        // Arrange
        var client = CreateClient(CreateFactory(db =>
        {
            AddUser(db, 2, "a@example.com");
        }));

        // Act
        var result = await GetGroupProgressAsync(client);

        // Assert
        Assert.Equal(0, result.UserCount);
        Assert.Equal(0, result.AvailableFlagCount);
        Assert.Equal(0, result.EarnedFlagCount);
        Assert.Null(result.EarnedPercent);
    }

    [Theory]
    [InlineData("", "2026-06-01T12:00:00Z")]
    [InlineData("?asOf=2026-05-30T10:00:00.000Z", "2026-05-30T10:00:00Z")]
    // Przesunięcie strefy jest normalizowane do UTC
    [InlineData("?asOf=2026-05-30T12:00:00%2B02:00", "2026-05-30T10:00:00Z")]
    public async Task GroupProgress_AsOfIsUtc(string query, string expectedAsOf)
    {
        // Arrange
        var client = CreateClient(CreateFactory());

        // Act
        var rawAsOf = await GetRawAsOfAsync(client, query);

        // Assert
        Assert.EndsWith("Z", rawAsOf);
        Assert.Equal(expectedAsOf, rawAsOf);
    }

    [Fact]
    public async Task GroupProgress_WithTooEarlyAsOf_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateClient(CreateFactory());

        // Act
        var response = await client.GetAsync(Url + "?asOf=1999-12-31T23:59:59Z");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GroupProgress_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateClient(CreateFactory(), withJwt: false);

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GroupProgress_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateClient(CreateFactory(), withXToken: false);

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}