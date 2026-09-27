using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

using App01.Modules.Portal.Features.MailFromClientGet;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace App01.Bootstrapper.Api.Tests.Features.Portal.MailFromClientGet;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string JwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";
    private const string ValidXToken = "test-x-token";

    public EndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private WebApplicationFactory<Program> CreateFactoryWithData(Action<AppDbContext>? seedData = null)
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
                    ["Tokens:X-TOKEN"] = ValidXToken,
                    ["ConnectionStrings:DefaultConnection"] = "Server=dummy;Database=dummy;Integrated Security=True;"
                });
            });

            builder.ConfigureServices(services =>
            {
                var hostedServicesToRemove = services
                    .Where(d => d.ServiceType == typeof(IHostedService))
                    .ToList();
                foreach (var descriptor in hostedServicesToRemove)
                    services.Remove(descriptor);

                var descriptorsToRemove = services
                    .Where(d => d.ServiceType.FullName != null &&
                               (d.ServiceType.FullName.Contains("DbContext") ||
                                d.ServiceType.FullName.Contains("EntityFramework")))
                    .ToList();
                foreach (var descriptor in descriptorsToRemove)
                    services.Remove(descriptor);

                var dbName = $"TestDb_{Guid.NewGuid()}";
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                if (seedData != null)
                {
                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.Database.EnsureCreated();
                    seedData(db);
                }
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
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private WebApplicationFactory<Program> CreateFactoryWithOneMail()
    {
        return CreateFactoryWithData(db =>
        {
            db.Mails.Add(new Mail
            {
                Id = 1,
                Email = "sender@example.com",
                Topic = "Test Topic",
                Body = "Test Body Content",
                CreatedAt = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc)
            });
            db.SaveChanges();
        });
    }

    [Fact]
    public async Task MailFromClientGet_WithoutJwtToken_ReturnsUnauthorized()
    {
        var client = CreateFactoryWithOneMail().CreateClient();

        var response = await client.GetAsync("/api/portal/mail-from-client-get?id=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MailFromClientGet_WithInvalidJwtToken_ReturnsUnauthorized()
    {
        var client = CreateFactoryWithOneMail().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        var response = await client.GetAsync("/api/portal/mail-from-client-get?id=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MailFromClientGet_WithNonAdminUser_ReturnsForbidden()
    {
        var client = CreateFactoryWithOneMail().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(2, "user@example.com", isAdmin: false));

        var response = await client.GetAsync("/api/portal/mail-from-client-get?id=1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MailFromClientGet_WithAdminAndExistingId_ReturnsOkWithFullData()
    {
        var client = CreateFactoryWithOneMail().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.GetAsync("/api/portal/mail-from-client-get?id=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("sender@example.com", result.Email);
        Assert.Equal("Test Topic", result.Topic);
        Assert.Equal("Test Body Content", result.Body);
    }

    [Fact]
    public async Task MailFromClientGet_WithNonExistentId_ReturnsNotFound()
    {
        var client = CreateFactoryWithOneMail().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.GetAsync("/api/portal/mail-from-client-get?id=9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MailFromClientGet_ReturnsBodyField()
    {
        var client = CreateFactoryWithOneMail().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.GetAsync("/api/portal/mail-from-client-get?id=1");

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Test Body Content", content);
    }

    [Fact]
    public async Task MailFromClientGet_ReturnsCreatedAt()
    {
        var client = CreateFactoryWithOneMail().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.GetAsync("/api/portal/mail-from-client-get?id=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(2025, result.CreatedAt.Year);
        Assert.Equal(1, result.CreatedAt.Month);
        Assert.Equal(15, result.CreatedAt.Day);
    }
}