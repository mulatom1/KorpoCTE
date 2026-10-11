using System.Data.Common;

using App01.Bootstrapper.Api.Tests.Infrastructure.SqlServer;
using App01.Shared.Application.Entities.Courses;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App01.Bootstrapper.Api.Tests.Features.Courses.ActivateFlag;

// Strażnik schematu na prawdziwym SQL Server: flaga zdobyta najwyżej raz na użytkownika jest wymuszona
// w bazie (PRD Guardrails: "flaga nie może zostać policzona drugi raz"). EF InMemory ignoruje indeksy
// unikalne, więc tylko te testy łapią usunięcie indeksu z konfiguracji lub migracji.
public class UserFlagUniquenessSqlServerTests : IClassFixture<SqlServerDatabaseFixture>
{
    private readonly SqlServerDatabaseFixture _database;

    public UserFlagUniquenessSqlServerTests(SqlServerDatabaseFixture database)
    {
        _database = database;
    }

    [SqlServerFact]
    public async Task UserFlags_UniqueIndexOnUserIdFlagId_ExistsAfterMigrations()
    {
        // Arrange - kontekst z hosta aplikacji: produkcyjna rejestracja UseSqlServer, baza z fixture'a
        await using var factory = new SqlServerWebApplicationFactory(_database);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(db.Database.IsSqlServer());
        Assert.Equal(_database.DatabaseName, db.Database.GetDbConnection().Database);

        // Act - kolumny kluczowe indeksów unikalnych tabeli UserFlags (bez klucza głównego)
        var uniqueIndexes = new Dictionary<string, List<string>>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT i.name, c.name
                FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE i.object_id = OBJECT_ID(N'[Courses].[UserFlags]')
                  AND i.is_unique = 1 AND i.is_primary_key = 0 AND ic.is_included_column = 0
                """;
            await using DbDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var indexName = reader.GetString(0);
                if (!uniqueIndexes.TryGetValue(indexName, out var columns))
                {
                    columns = [];
                    uniqueIndexes[indexName] = columns;
                }
                columns.Add(reader.GetString(1));
            }
        }

        // Assert - istnieje indeks unikalny dokładnie na parze (UserId, FlagId)
        Assert.Contains(uniqueIndexes.Values, columns =>
            columns.Order().SequenceEqual(new[] { "FlagId", "UserId" }));
    }

    [SqlServerFact]
    public async Task UserFlags_DuplicateRowInsertedDirectly_IsRejectedByDatabase()
    {
        // Arrange
        var (userId, otherUserId, flagId) = await SeedUsersAndFlagAsync();

        await using (var first = _database.CreateDbContext())
        {
            first.UserFlags.Add(new UserFlag { UserId = userId, FlagId = flagId, EarnedAt = DateTime.UtcNow });
            await first.SaveChangesAsync();
        }

        // Act - drugi zapis tej samej pary z osobnego kontekstu (jak równoległy request)
        await using var second = _database.CreateDbContext();
        second.UserFlags.Add(new UserFlag { UserId = userId, FlagId = flagId, EarnedAt = DateTime.UtcNow });
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        // Assert - odrzucenie przez indeks unikalny (2601) lub ograniczenie unikalności (2627)
        var sqlException = Assert.IsType<SqlException>(exception.InnerException);
        Assert.Contains(sqlException.Number, new[] { 2601, 2627 });

        // Ta sama flaga u innego użytkownika jest dozwolona - indeks nie jest na samym FlagId
        await using (var third = _database.CreateDbContext())
        {
            third.UserFlags.Add(new UserFlag { UserId = otherUserId, FlagId = flagId, EarnedAt = DateTime.UtcNow });
            await third.SaveChangesAsync();
        }

        await using var verify = _database.CreateDbContext();
        Assert.Equal(1, await verify.UserFlags.CountAsync(uf => uf.UserId == userId && uf.FlagId == flagId));
        Assert.Equal(2, await verify.UserFlags.CountAsync(uf => uf.FlagId == flagId));
    }

    // Id z bazy (identity) - migracja PortalFirstUserAdmin seeduje już jednego użytkownika
    private async Task<(long UserId, long OtherUserId, int FlagId)> SeedUsersAndFlagAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var db = _database.CreateDbContext();

        var user = new User { Email = $"a-{suffix}@example.com", CreatedAt = DateTime.UtcNow };
        var otherUser = new User { Email = $"b-{suffix}@example.com", CreatedAt = DateTime.UtcNow };
        var course = new Course { Slug = $"kurs-{suffix}", PublishDate = DateTime.UtcNow.AddDays(-1) };
        db.Users.AddRange(user, otherUser);
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var flag = new Flag { CourseId = course.Id, Code = $"KOD-{suffix}", Title = "Zadanie" };
        db.Flags.Add(flag);
        await db.SaveChangesAsync();

        return (user.Id, otherUser.Id, flag.Id);
    }
}