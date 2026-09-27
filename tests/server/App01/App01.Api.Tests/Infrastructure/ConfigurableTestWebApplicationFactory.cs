using App01.Shared.Infrastructure.Repositories;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App01.Bootstrapper.Api.Tests.Infrastructure;

public class ConfigurableTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _configurationOverrides;

    public ConfigurableTestWebApplicationFactory(Dictionary<string, string?> configurationOverrides)
    {
        _configurationOverrides = configurationOverrides;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Configure test environment first
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();

            // Default configuration for tests - must include connection string
            var defaultConfig = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "ThisIsASecretKeyForTestingPurposesOnly123456",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Swagger:Enabled"] = "false",
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=InMemoryTestDb;Integrated Security=True;"
            };

            // Merge with test-specific overrides
            foreach (var kvp in _configurationOverrides)
            {
                defaultConfig[kvp.Key] = kvp.Value;
            }

            config.AddInMemoryCollection(defaultConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add InMemory database for testing
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(databaseName: $"InMemoryDbForTesting_{Guid.NewGuid()}");
                options.UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()));
            });

            // Build the service provider and create the database
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });
    }
}