using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace App01.Bootstrapper.Api.Tests.Infrastructure.SqlServer;

/// <summary>
/// Host aplikacji na prawdziwym SQL Server. DbContext NIE jest podmieniany - zostaje produkcyjna
/// rejestracja UseSqlServer + UseCompatibilityLevel(110) z SharedInfrastructureDI, dostaje tylko
/// connection string do bazy z SqlServerDatabaseFixture.
/// </summary>
public sealed class SqlServerWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string JwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";
    private const string XToken = "test-x-token";

    private readonly string _connectionString;

    public SqlServerWebApplicationFactory(SqlServerDatabaseFixture database)
    {
        _connectionString = database.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        // UseSetting, nie ConfigureAppConfiguration: SharedInfrastructureDI czyta connection string już
        // przy rejestracji serwisów, a w środowisku Test jego brak po cichu pomija rejestrację DbContext
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = JwtKey,
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Jwt:ExpiryInMinutes"] = "60",
                ["Swagger:Enabled"] = "false",
                ["Tokens:X-TOKEN"] = XToken
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
        });
    }

    public HttpClient CreateClientFor(long userId, string email, bool isAdmin = false)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", XToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId, email, isAdmin));
        return client;
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
}