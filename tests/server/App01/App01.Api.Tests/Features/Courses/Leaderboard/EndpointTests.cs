using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

using App01.Modules.Courses.Features.Leaderboard;
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


namespace App01.Bootstrapper.Api.Tests.Features.Courses.Leaderboard;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string JwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";
    private const string XToken = "test-x-token";
    private const string Url = "/api/courses/leaderboard";
    private const long CurrentUserId = 2;

    // Flagi: 10-13 w kursie opublikowanym, 20 w kursie z PublishDate == teraz, 30 w kursie przyszłym
    private const int PublishedFlagA = 10;
    private const int PublishedFlagB = 11;
    private const int PublishedFlagC = 12;
    private const int PublishedFlagD = 13;
    private const int NowFlag = 20;
    private const int FutureFlag = 30;

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

    // Kursy: 1 (opublikowany), 2 (PublishDate == teraz - opublikowany), 3 (przyszły)
    private static void SeedCourses(AppDbContext db)
    {
        db.Courses.AddRange(
            new Course { Id = 1, Slug = "kurs-opublikowany", PublishDate = Now.AddDays(-10) },
            new Course { Id = 2, Slug = "kurs-teraz", PublishDate = Now },
            new Course { Id = 3, Slug = "kurs-przyszly", PublishDate = Now.AddMinutes(1) });

        db.Flags.AddRange(
            new Flag { Id = PublishedFlagA, CourseId = 1, Code = "KOD-10", Title = "Flaga 10" },
            new Flag { Id = PublishedFlagB, CourseId = 1, Code = "KOD-11", Title = "Flaga 11" },
            new Flag { Id = PublishedFlagC, CourseId = 1, Code = "KOD-12", Title = "Flaga 12" },
            new Flag { Id = PublishedFlagD, CourseId = 1, Code = "KOD-13", Title = "Flaga 13" },
            new Flag { Id = NowFlag, CourseId = 2, Code = "KOD-20", Title = "Flaga 20" },
            new Flag { Id = FutureFlag, CourseId = 3, Code = "KOD-30", Title = "Flaga 30" });
    }

    private static void AddUser(AppDbContext db, long id, string email, bool isAdmin = false)
    {
        db.Users.Add(new User { Id = id, Email = email, IsAdmin = isAdmin, CreatedAt = Now });
    }

    private static void AddUserFlag(AppDbContext db, long userId, int flagId, DateTime earnedAt)
    {
        db.UserFlags.Add(new UserFlag { UserId = userId, FlagId = flagId, EarnedAt = earnedAt });
    }

    // Seed: kursy i flagi + użytkownicy i zdobyte flagi z przekazanej akcji
    private static Action<AppDbContext> Seed(Action<AppDbContext> seedUsers)
    {
        return db =>
        {
            SeedCourses(db);
            seedUsers(db);
        };
    }

    private static async Task<Contracts.Response> GetLeaderboardAsync(HttpClient client, string query = "")
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
    public async Task Leaderboard_OrdersByFlagCountDesc()
    {
        // Arrange
        var client = CreateClient(CreateFactory(Seed(db =>
        {
            AddUser(db, 2, "jeden@example.com");
            AddUser(db, 3, "trzy@example.com");
            AddUser(db, 4, "dwa@example.com");
            AddUserFlag(db, 2, PublishedFlagA, Now.AddDays(-5));
            AddUserFlag(db, 3, PublishedFlagA, Now.AddDays(-1));
            AddUserFlag(db, 3, PublishedFlagB, Now.AddDays(-1));
            AddUserFlag(db, 3, PublishedFlagC, Now.AddDays(-1));
            AddUserFlag(db, 4, PublishedFlagA, Now.AddDays(-1));
            AddUserFlag(db, 4, PublishedFlagB, Now.AddDays(-1));
        })));

        // Act
        var result = await GetLeaderboardAsync(client);

        // Assert
        Assert.Equal(
            [
                new Contracts.LeaderboardEntryDto(1, "trzy", 3),
                new Contracts.LeaderboardEntryDto(2, "dwa", 2),
                new Contracts.LeaderboardEntryDto(3, "jeden", 1)
            ],
            result.Entries);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task Leaderboard_CountsOnlyPublishedCourses()
    {
        // Arrange
        var client = CreateClient(CreateFactory(Seed(db =>
        {
            AddUser(db, 2, "anna@example.com");
            AddUserFlag(db, 2, PublishedFlagA, Now.AddDays(-2));
            // Kurs z PublishDate == teraz jest liczony
            AddUserFlag(db, 2, NowFlag, Now);
            // Kurs przyszły nie jest liczony
            AddUserFlag(db, 2, FutureFlag, Now.AddHours(-1));
        })));

        // Act
        var result = await GetLeaderboardAsync(client);

        // Assert
        var entry = Assert.Single(result.Entries);
        Assert.Equal(new Contracts.LeaderboardEntryDto(1, "anna", 2), entry);
    }

    [Fact]
    public async Task Leaderboard_SkipsUsersWithoutCountedFlags()
    {
        // Arrange
        var client = CreateClient(CreateFactory(Seed(db =>
        {
            AddUser(db, 2, "zflaga@example.com");
            AddUser(db, 3, "bezflag@example.com");
            AddUser(db, 4, "tylkoprzyszle@example.com");
            AddUserFlag(db, 2, PublishedFlagA, Now.AddDays(-1));
            AddUserFlag(db, 4, FutureFlag, Now.AddDays(-1));
        })));

        // Act
        var result = await GetLeaderboardAsync(client);

        // Assert
        var entry = Assert.Single(result.Entries);
        Assert.Equal("zflaga", entry.DisplayName);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Leaderboard_IncludesAdmins()
    {
        // Arrange
        var client = CreateClient(CreateFactory(Seed(db =>
        {
            AddUser(db, 2, "uczestnik@example.com");
            AddUser(db, 5, "admin@example.com", isAdmin: true);
            AddUserFlag(db, 2, PublishedFlagA, Now.AddDays(-1));
            AddUserFlag(db, 5, PublishedFlagA, Now.AddDays(-2));
            AddUserFlag(db, 5, PublishedFlagB, Now.AddDays(-2));
        })));

        // Act
        var result = await GetLeaderboardAsync(client);

        // Assert
        Assert.Equal(
            [
                new Contracts.LeaderboardEntryDto(1, "admin", 2),
                new Contracts.LeaderboardEntryDto(2, "uczestnik", 1)
            ],
            result.Entries);
    }

    [Fact]
    public async Task Leaderboard_TiesShareRankAndEarlierLastFlagFirst()
    {
        // Arrange
        var client = CreateClient(CreateFactory(Seed(db =>
        {
            // Późniejszy w remisie ma mniejsze Id - kolejność wynika z daty ostatniej flagi, nie z Id
            AddUser(db, 2, "pozniej@example.com");
            AddUser(db, 3, "wczesniej@example.com");
            AddUser(db, 4, "trzeci@example.com");
            AddUserFlag(db, 2, PublishedFlagA, Now.AddDays(-5));
            AddUserFlag(db, 2, PublishedFlagB, Now.AddDays(-1));
            AddUserFlag(db, 3, PublishedFlagA, Now.AddDays(-4));
            AddUserFlag(db, 3, PublishedFlagB, Now.AddDays(-3));
            // Flaga kursu przyszłego nie przesuwa daty ostatniej flagi
            AddUserFlag(db, 3, FutureFlag, Now.AddHours(-1));
            AddUserFlag(db, 4, PublishedFlagA, Now.AddDays(-10));
        })));

        // Act
        var result = await GetLeaderboardAsync(client);

        // Assert
        Assert.Equal(
            [
                new Contracts.LeaderboardEntryDto(1, "wczesniej", 2),
                new Contracts.LeaderboardEntryDto(1, "pozniej", 2),
                new Contracts.LeaderboardEntryDto(3, "trzeci", 1)
            ],
            result.Entries);
    }

    [Theory]
    [InlineData(1, new[] { "a", "b" }, new[] { 1, 2 })]
    [InlineData(2, new[] { "c", "d" }, new[] { 2, 4 })]
    [InlineData(3, new string[0], new int[0])]
    public async Task Leaderboard_RankContinuesAcrossPages(int page, string[] expectedNames, int[] expectedRanks)
    {
        // Arrange
        // a: 3 flagi; b i c: remis po 2 (b wcześniej) na granicy stron; d: 1 flaga
        var client = CreateClient(CreateFactory(Seed(db =>
        {
            AddUser(db, 2, "a@example.com");
            AddUser(db, 3, "b@example.com");
            AddUser(db, 4, "c@example.com");
            AddUser(db, 5, "d@example.com");
            AddUserFlag(db, 2, PublishedFlagA, Now.AddDays(-1));
            AddUserFlag(db, 2, PublishedFlagB, Now.AddDays(-1));
            AddUserFlag(db, 2, PublishedFlagC, Now.AddDays(-1));
            AddUserFlag(db, 3, PublishedFlagA, Now.AddDays(-3));
            AddUserFlag(db, 3, PublishedFlagB, Now.AddDays(-3));
            AddUserFlag(db, 4, PublishedFlagA, Now.AddDays(-2));
            AddUserFlag(db, 4, PublishedFlagB, Now.AddDays(-2));
            AddUserFlag(db, 5, PublishedFlagD, Now.AddDays(-9));
        })));

        // Act
        var result = await GetLeaderboardAsync(client, $"?page={page}&pageSize=2");

        // Assert
        Assert.Equal(expectedNames, result.Entries.Select(e => e.DisplayName));
        Assert.Equal(expectedRanks, result.Entries.Select(e => e.Rank));
        Assert.Equal(page, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task Leaderboard_DisplayNameIsEmailLocalPart()
    {
        // Arrange
        var client = CreateClient(CreateFactory(Seed(db =>
        {
            AddUser(db, 2, "jan.kowalski@firma.pl");
            AddUser(db, 3, "bezmalpy");
            AddUser(db, 4, "dwie@malpy@firma.pl");
            AddUser(db, 5, "");
            AddUserFlag(db, 2, PublishedFlagA, Now.AddDays(-4));
            AddUserFlag(db, 3, PublishedFlagA, Now.AddDays(-3));
            AddUserFlag(db, 4, PublishedFlagA, Now.AddDays(-2));
            AddUserFlag(db, 5, PublishedFlagA, Now.AddDays(-1));
        })));

        // Act
        var result = await GetLeaderboardAsync(client);

        // Assert
        Assert.Equal(["jan.kowalski", "bezmalpy", "dwie", "—"], result.Entries.Select(e => e.DisplayName));
    }

    [Fact]
    public async Task Leaderboard_ResponseDoesNotContainEmailDomainOrUserId()
    {
        // Arrange
        var client = CreateClient(CreateFactory(Seed(db =>
        {
            AddUser(db, 2, "jan.kowalski@firma.pl");
            AddUser(db, 3, "ola@example.com", isAdmin: true);
            AddUserFlag(db, 2, PublishedFlagA, Now.AddDays(-1));
            AddUserFlag(db, 3, PublishedFlagA, Now.AddDays(-2));
        })));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("jan.kowalski", json);
        Assert.DoesNotContain("@", json);
        Assert.DoesNotContain("firma.pl", json);
        Assert.DoesNotContain("example.com", json);
        Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Leaderboard_WithoutFlags_ReturnsEmptyList()
    {
        // Arrange
        var client = CreateClient(CreateFactory(Seed(db =>
        {
            AddUser(db, 2, "user1@example.com");
        })));

        // Act
        var result = await GetLeaderboardAsync(client);

        // Assert
        Assert.Empty(result.Entries);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    public async Task Leaderboard_WithInvalidPaging_ReturnsBadRequest(string query)
    {
        // Arrange
        var client = CreateClient(CreateFactory());

        // Act
        var response = await client.GetAsync(Url + query);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Leaderboard_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateClient(CreateFactory(), withJwt: false);

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Leaderboard_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateClient(CreateFactory(), withXToken: false);

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}