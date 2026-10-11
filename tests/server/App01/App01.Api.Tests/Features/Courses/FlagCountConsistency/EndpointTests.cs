using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

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

using GroupProgressContracts = App01.Modules.Courses.Features.GroupProgress.Contracts;
using HangarContracts = App01.Modules.Courses.Features.HangarFlags.Contracts;
using LeaderboardContracts = App01.Modules.Courses.Features.Leaderboard.Contracts;

namespace App01.Bootstrapper.Api.Tests.Features.Courses.FlagCountConsistency;

// Test krzyżowy: jeden stan bazy, trzy widoki liczące flagi. Wyrocznia z PRD, nie z kodu handlerów:
// - FR-008: ranking według liczby zdobytych flag - "ta sama liczba, którą uczestnik widzi w hangarze";
// - FR-009 + kryterium sukcesu: procent = różne flagi zdobyte przez grupę / flagi opublikowanych kursów;
// - FR-003: kurs opublikowany = data publikacji równa "teraz" lub wcześniejsza (granica włączna).
// Oczekiwane liczby są policzone ręcznie z seedu poniżej, nie z obliczeń produkcyjnych.
public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string JwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";
    private const string XToken = "test-x-token";

    // Stała chwila "teraz" dla testów
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    // Uczestnicy U2, U3, U5 i administrator U4
    private const long U2 = 2;
    private const long U3 = 3;
    private const long U4Admin = 4;
    private const long U5 = 5;

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
        // Stała nazwa bazy na fabrykę - seed i żądania trzech endpointów widzą te same dane
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

    // Kurs K1 opublikowany 10 dni temu: F1-F4 (F4 bez kryteriów - też jest flagą do zdobycia);
    // kurs K2 opublikowany dokładnie "teraz": F5 (granica - liczony, FR-003);
    // kurs K3 opublikowany za minutę: F6 (nieopublikowany - nigdzie nie liczony).
    // U2: F1, F2 i F5 zdobyta dokładnie "teraz"; U3: F1 oraz F6 z kursu nieopublikowanego;
    // U4 (administrator, liczony jak każdy uczestnik): F3; U5: brak flag.
    private static void SeedData(AppDbContext db)
    {
        db.Users.AddRange(
            new User { Id = U2, Email = "u2@example.com", CreatedAt = Now },
            new User { Id = U3, Email = "u3@example.com", CreatedAt = Now },
            new User { Id = U4Admin, Email = "u4@example.com", IsAdmin = true, CreatedAt = Now },
            new User { Id = U5, Email = "u5@example.com", CreatedAt = Now });

        db.Courses.AddRange(
            new Course { Id = 1, Slug = "k1", PublishDate = Now.AddDays(-10) },
            new Course { Id = 2, Slug = "k2", PublishDate = Now },
            new Course { Id = 3, Slug = "k3", PublishDate = Now.AddMinutes(1) });

        db.Flags.AddRange(
            new Flag { Id = 1, CourseId = 1, Code = "KOD-F1", Title = "F1", Criteria = "kryteria" },
            new Flag { Id = 2, CourseId = 1, Code = "KOD-F2", Title = "F2", Criteria = "kryteria" },
            new Flag { Id = 3, CourseId = 1, Code = "KOD-F3", Title = "F3", Criteria = "kryteria" },
            new Flag { Id = 4, CourseId = 1, Code = "KOD-F4", Title = "F4", Criteria = null },
            new Flag { Id = 5, CourseId = 2, Code = "KOD-F5", Title = "F5", Criteria = "kryteria" },
            new Flag { Id = 6, CourseId = 3, Code = "KOD-F6", Title = "F6", Criteria = "kryteria" });

        db.UserFlags.AddRange(
            new UserFlag { UserId = U2, FlagId = 1, EarnedAt = Now.AddDays(-5) },
            new UserFlag { UserId = U2, FlagId = 2, EarnedAt = Now.AddDays(-1) },
            new UserFlag { UserId = U2, FlagId = 5, EarnedAt = Now },
            new UserFlag { UserId = U3, FlagId = 1, EarnedAt = Now.AddDays(-3) },
            new UserFlag { UserId = U3, FlagId = 6, EarnedAt = Now.AddDays(-1) },
            new UserFlag { UserId = U4Admin, FlagId = 3, EarnedAt = Now.AddDays(-2) });
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

    private static HttpClient CreateClientFor(WebApplicationFactory<Program> factory, long userId, bool isAdmin = false)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", XToken);
        var token = GenerateJwtToken(userId, $"u{userId}@example.com", isAdmin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<T> GetOkAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Fallback SPA też zwraca 200 - sprawdzamy, że to JSON z kontraktu
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var result = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(result);
        return result;
    }

    [Fact]
    public async Task FlagCounts_OneSeed_HangarLeaderboardAndDashboardAgree()
    {
        // Arrange
        var factory = CreateFactory();
        var users = new[] { (Id: U2, IsAdmin: false), (Id: U3, IsAdmin: false), (Id: U4Admin, IsAdmin: true), (Id: U5, IsAdmin: false) };

        // Act - hangar każdego uczestnika (tylko zdobyte, liczniki niezależne od filtra), ranking i dashboard
        var hangars = new Dictionary<long, HangarContracts.Response>();
        foreach (var (id, isAdmin) in users)
        {
            hangars[id] = await GetOkAsync<HangarContracts.Response>(
                CreateClientFor(factory, id, isAdmin), "/api/courses/hangar-flags?filter=Earned&page=1&pageSize=100");
        }

        var client = CreateClientFor(factory, U2);
        var ranking = await GetOkAsync<LeaderboardContracts.Response>(client, "/api/courses/leaderboard?page=1&pageSize=100");
        var dashboard = await GetOkAsync<GroupProgressContracts.Response>(client, "/api/courses/group-progress");

        // Assert - wyrocznia literalna (F1-F5 opublikowane, F6 nie)
        Assert.All(hangars.Values, h => Assert.Equal(5, h.AllCount));
        Assert.Equal(3, hangars[U2].EarnedCount);
        Assert.Equal(1, hangars[U3].EarnedCount);
        Assert.Equal(1, hangars[U4Admin].EarnedCount);
        Assert.Equal(0, hangars[U5].EarnedCount);

        var rankingByUser = ranking.Entries.ToDictionary(e => e.DisplayName, e => e.FlagCount);
        Assert.Equal(new Dictionary<string, int> { ["u2"] = 3, ["u3"] = 1, ["u4"] = 1 }, rankingByUser);
        Assert.Equal(3, ranking.TotalCount);

        Assert.Equal(3, dashboard.UserCount);
        Assert.Equal(5, dashboard.AvailableFlagCount);
        Assert.Equal(4, dashboard.EarnedFlagCount);
        Assert.Equal(80.0, dashboard.EarnedPercent);

        // Assert - tożsamości krzyżowe z PRD (dryf jednego z handlerów łamie co najmniej jedną)
        foreach (var (id, _) in users)
        {
            var inRanking = rankingByUser.GetValueOrDefault($"u{id}", 0);
            Assert.Equal(hangars[id].EarnedCount, inRanking);
        }
        Assert.Equal(hangars[U2].AllCount, dashboard.AvailableFlagCount);
        Assert.Equal(ranking.TotalCount, dashboard.UserCount);
        var distinctEarnedInHangars = hangars.Values.SelectMany(h => h.Flags).Select(f => f.FlagId).Distinct().Count();
        Assert.Equal(distinctEarnedInHangars, dashboard.EarnedFlagCount);
    }
}