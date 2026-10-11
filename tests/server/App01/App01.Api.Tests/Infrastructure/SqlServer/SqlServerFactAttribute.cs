namespace App01.Bootstrapper.Api.Tests.Infrastructure.SqlServer;

/// <summary>
/// Test na prawdziwym SQL Server. Bez zmiennej APP01_SQLSERVER_TESTS (lokalnie i w CI) test jest pomijany.
/// Zmienna zawiera connection string serwera BEZ Database=, np.
/// Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=True
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public const string ConnectionStringVariable = "APP01_SQLSERVER_TESTS";

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable)))
        {
            Skip = $"Ustaw zmienną {ConnectionStringVariable} (connection string serwera bez Database=), aby uruchomić testy na prawdziwym SQL Server.";
        }
    }
}