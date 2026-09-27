using System.Net.Http.Json;

using App01.Bootstrapper.Api.Tests.Infrastructure;
using App01.Modules.Portal.Features.GetApiVersion;

namespace App01.Bootstrapper.Api.Tests.Features.Portal.GetApiVersion;

public class EndpointTests : IDisposable
{
    [Fact]
    public async Task GetApiVersion_ReturnsOkWithVersion()
    {
        // Arrange
        var factory = new ConfigurableTestWebApplicationFactory(new Dictionary<string, string?>
        {
            ["ApiVersion"] = "1.0.0"
        });

        var client = factory.CreateClient();

        try
        {
            // Act
            var response = await client.GetAsync("/api/portal/get-api-version");

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
            Assert.NotNull(result);
            Assert.Equal("1.0.0", result.Version);
        }
        finally
        {
            client.Dispose();
            factory.Dispose();
        }
    }

    [Fact]
    public async Task GetApiVersion_ReturnsEmptyVersion_WhenNotConfigured()
    {
        // Arrange
        var factory = new ConfigurableTestWebApplicationFactory(new Dictionary<string, string?>
        {
            // Explicitly not setting ApiVersion
        });

        var client = factory.CreateClient();

        try
        {
            // Act
            var response = await client.GetAsync("/api/portal/get-api-version");

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
            Assert.NotNull(result);
            Assert.Equal("Version not found", result.Version);
        }
        finally
        {
            client.Dispose();
            factory.Dispose();
        }
    }

    [Theory]
    [InlineData("2.5.3")]
    [InlineData("1.0.0-beta")]
    [InlineData("v3.2.1")]
    public async Task GetApiVersion_ReturnsConfiguredVersion(string expectedVersion)
    {
        // Arrange
        var factory = new ConfigurableTestWebApplicationFactory(new Dictionary<string, string?>
        {
            ["ApiVersion"] = expectedVersion
        });

        var client = factory.CreateClient();

        try
        {
            // Act
            var response = await client.GetAsync("/api/portal/get-api-version");

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
            Assert.NotNull(result);
            Assert.Equal(expectedVersion, result.Version);
        }
        finally
        {
            client.Dispose();
            factory.Dispose();
        }
    }

    public void Dispose()
    {
        // Implementation for IDisposable if needed
    }
}