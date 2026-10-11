using App01.Shared.Infrastructure.Repositories;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace App01.Bootstrapper.Api.Tests.Infrastructure.SqlServer;

/// <summary>
/// Osobna baza na klasę testów: jawna kolacja i poziom zgodności 110 (docelowy SQL Server 2012),
/// schemat z produkcyjnych migracji (Migrate, nie EnsureCreated - dowodzi, że indeksy są w migracjach).
/// Bez zmiennej APP01_SQLSERVER_TESTS nic nie robi - testy są wtedy pomijane przez SqlServerFactAttribute.
/// </summary>
public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    private const string Collation = "SQL_Latin1_General_CP1_CI_AS";

    private readonly string? _serverConnectionString =
        Environment.GetEnvironmentVariable(SqlServerFactAttribute.ConnectionStringVariable);

    public string DatabaseName { get; } = $"App01Tests_{Guid.NewGuid():N}";

    public string ConnectionString { get; private set; } = string.Empty;

    private bool IsConfigured => !string.IsNullOrWhiteSpace(_serverConnectionString);

    public async Task InitializeAsync()
    {
        if (!IsConfigured)
        {
            return;
        }

        // Baza zakładana przez master przed Migrate - inaczej powstałaby z kolacją serwera
        await using (var master = new SqlConnection(BuildConnectionString("master")))
        {
            await master.OpenAsync();
            await ExecuteAsync(master, $"CREATE DATABASE [{DatabaseName}] COLLATE {Collation}");
            await ExecuteAsync(master, $"ALTER DATABASE [{DatabaseName}] SET COMPATIBILITY_LEVEL = 110");
        }

        ConnectionString = BuildConnectionString(DatabaseName);

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString, sql => sql.UseCompatibilityLevel(110))
            .Options;
        return new AppDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (!IsConfigured)
        {
            return;
        }

        // Pula połączeń trzyma bazę - bez czyszczenia DROP się nie powiedzie
        SqlConnection.ClearAllPools();

        await using var master = new SqlConnection(BuildConnectionString("master"));
        await master.OpenAsync();
        await ExecuteAsync(master,
            $"IF DB_ID(N'{DatabaseName}') IS NOT NULL " +
            $"BEGIN ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]; END");
    }

    private string BuildConnectionString(string database)
    {
        var builder = new SqlConnectionStringBuilder(_serverConnectionString)
        {
            InitialCatalog = database
        };
        return builder.ConnectionString;
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}