using System.Net;
using System.Net.Http.Json;

using App01.Bootstrapper.Api.Tests.Infrastructure.SqlServer;
using App01.Shared.Application.Entities.Courses;
using App01.Shared.Application.Entities.Portal;

using Microsoft.EntityFrameworkCore;

using ActivateContracts = App01.Modules.Courses.Features.ActivateFlag.Contracts;
using LeaderboardContracts = App01.Modules.Courses.Features.Leaderboard.Contracts;

namespace App01.Bootstrapper.Api.Tests.Features.Courses.ActivateFlag;

// Wyścig aktywacji na prawdziwym SQL Server (PRD Guardrails: flaga raz zdobyta nie może zostać
// policzona drugi raz; FR-004). Realne równoległe żądania HTTP, nie mock - drugi zapis musi odrzucić
// indeks unikalny w bazie, a handler ma to zgłosić jako AlreadyOwned, nie jako błąd.
// Ranking liczy wiersze UserFlags, więc duplikat byłby w nim widoczny jako 2.
public class ActivateFlagRaceSqlServerTests : IClassFixture<SqlServerDatabaseFixture>
{
    private const string ActivateUrl = "/api/courses/activate-flag";
    private const string LeaderboardUrl = "/api/courses/leaderboard?page=1&pageSize=100";

    private readonly SqlServerDatabaseFixture _database;

    public ActivateFlagRaceSqlServerTests(SqlServerDatabaseFixture database)
    {
        _database = database;
    }

    [SqlServerFact]
    public async Task ActivateFlag_TenParallelRequestsSameUser_ExactlyOneRowAndRankingShowsOne()
    {
        // Arrange
        var flag = await SeedPublishedFlagAsync();
        var user = await SeedUserAsync();
        await using var factory = new SqlServerWebApplicationFactory(_database);
        using var client = factory.CreateClientFor(user.Id, user.Email);
        await WarmUpAsync(client);

        // Act
        var results = await ActivateInParallelAsync(Enumerable.Repeat(client, 10), flag.Code);

        // Assert - same 200 (zero błędów 500), dokładnie jedna aktywacja, reszta AlreadyOwned
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, results.Count(r => r.Body?.Status == ActivateContracts.Statuses.Activated));
        Assert.Equal(9, results.Count(r => r.Body?.Status == ActivateContracts.Statuses.AlreadyOwned));

        await using var db = _database.CreateDbContext();
        Assert.Equal(1, await db.UserFlags.CountAsync(uf => uf.UserId == user.Id && uf.FlagId == flag.Id));

        var ranking = await GetLeaderboardAsync(client);
        Assert.Equal(1, Assert.Single(ranking.Entries, e => e.DisplayName == LocalPart(user.Email)).FlagCount);
    }

    [SqlServerFact]
    public async Task ActivateFlag_ParallelRequestsDifferentUsers_EachGetsOwnRow()
    {
        // Arrange - ta sama flaga, dwóch użytkowników, po 5 równoległych żądań każdy
        var flag = await SeedPublishedFlagAsync();
        var first = await SeedUserAsync();
        var second = await SeedUserAsync();
        await using var factory = new SqlServerWebApplicationFactory(_database);
        using var firstClient = factory.CreateClientFor(first.Id, first.Email);
        using var secondClient = factory.CreateClientFor(second.Id, second.Email);
        await WarmUpAsync(firstClient);

        var clients = Enumerable.Repeat(firstClient, 5).Concat(Enumerable.Repeat(secondClient, 5)).ToList();

        // Act
        var results = await ActivateInParallelAsync(clients, flag.Code);

        // Assert - każdy użytkownik dostaje swoją jedną aktywację; indeks nie blokuje innych użytkowników
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, results.Take(5).Count(r => r.Body?.Status == ActivateContracts.Statuses.Activated));
        Assert.Equal(1, results.Skip(5).Count(r => r.Body?.Status == ActivateContracts.Statuses.Activated));
        Assert.Equal(8, results.Count(r => r.Body?.Status == ActivateContracts.Statuses.AlreadyOwned));

        await using var db = _database.CreateDbContext();
        Assert.Equal(2, await db.UserFlags.CountAsync(uf => uf.FlagId == flag.Id));

        var ranking = await GetLeaderboardAsync(firstClient);
        Assert.Equal(1, Assert.Single(ranking.Entries, e => e.DisplayName == LocalPart(first.Email)).FlagCount);
        Assert.Equal(1, Assert.Single(ranking.Entries, e => e.DisplayName == LocalPart(second.Email)).FlagCount);
    }

    // Wszystkie żądania startują z jednej bramki, żeby trafić w okno między sprawdzeniem a zapisem
    private static async Task<List<(HttpStatusCode StatusCode, ActivateContracts.Response? Body)>> ActivateInParallelAsync(
        IEnumerable<HttpClient> clients, string code)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = clients.Select(client => Task.Run(async () =>
        {
            await gate.Task;
            var response = await client.PostAsJsonAsync(ActivateUrl, new ActivateContracts.Request(code));
            var body = response.StatusCode == HttpStatusCode.OK
                ? await response.Content.ReadFromJsonAsync<ActivateContracts.Response>()
                : null;
            return (response.StatusCode, body);
        })).ToList();

        gate.SetResult();
        return (await Task.WhenAll(tasks)).ToList();
    }

    // Pierwsze żądanie uruchamia host i kompiluje model EF - poza mierzonym wyścigiem
    private static async Task WarmUpAsync(HttpClient client)
    {
        var response = await client.GetAsync(LeaderboardUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<LeaderboardContracts.Response> GetLeaderboardAsync(HttpClient client)
    {
        var response = await client.GetAsync(LeaderboardUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LeaderboardContracts.Response>();
        Assert.NotNull(result);
        return result;
    }

    // Kurs opublikowany wczoraj (zegar rzeczywisty - host używa TimeProvider.System); unikalny kod na test
    private async Task<Flag> SeedPublishedFlagAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var db = _database.CreateDbContext();

        var course = new Course { Slug = $"kurs-{suffix}", PublishDate = DateTime.UtcNow.AddDays(-1) };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var flag = new Flag { CourseId = course.Id, Code = $"KOD-{suffix}", Title = "Zadanie wyścigu" };
        db.Flags.Add(flag);
        await db.SaveChangesAsync();
        return flag;
    }

    // Id z bazy (identity) - migracja PortalFirstUserAdmin seeduje już jednego użytkownika
    private async Task<User> SeedUserAsync()
    {
        await using var db = _database.CreateDbContext();
        var user = new User { Email = $"u{Guid.NewGuid():N}@example.com", CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static string LocalPart(string email) => email[..email.IndexOf('@')];
}