using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

using App01.Modules.Courses.Features.ModuleHello;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Courses.ModuleHello;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string JwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";
    private const string XToken = "test-x-token";
    private const string Url = "/api/courses/module-hello";

    public EndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
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

                var dbName = $"TestDb_{Guid.NewGuid()}";
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName);
                });
            });
        });
    }

    private static string GenerateJwtToken(int userId, string email, bool isAdmin)
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

    [Fact]
    public async Task ModuleHello_WithJwtAndXToken_ReturnsOkWithMessage()
    {
        // Arrange
        var client = CreateFactory().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", XToken);
        var token = GenerateJwtToken(2, "user1@example.com", isAdmin: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal("Hello from module Courses!", result.Message);
    }

    [Fact]
    public async Task ModuleHello_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactory().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", XToken);

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ModuleHello_WithInvalidJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactory().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", XToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ModuleHello_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactory().CreateClient();
        var token = GenerateJwtToken(2, "user1@example.com", isAdmin: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}