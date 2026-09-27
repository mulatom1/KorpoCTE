using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;
using App01.Modules.Portal.Features.MailFromClientDelete;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace App01.Bootstrapper.Api.Tests.Features.Portal.MailFromClientDelete;

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

    private WebApplicationFactory<Program> CreateFactoryWithTwoMails()
    {
        return CreateFactoryWithData(db =>
        {
            db.Mails.Add(new Mail { Id = 1, Email = "a@example.com", Topic = "Topic A", Body = "Body A", CreatedAt = DateTime.UtcNow });
            db.Mails.Add(new Mail { Id = 2, Email = "b@example.com", Topic = "Topic B", Body = "Body B", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        });
    }

    [Fact]
    public async Task MailFromClientDelete_WithoutJwtToken_ReturnsUnauthorized()
    {
        var client = CreateFactoryWithTwoMails().CreateClient();

        var response = await client.DeleteAsync("/api/portal/mail-from-client-delete?id=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MailFromClientDelete_WithInvalidJwtToken_ReturnsUnauthorized()
    {
        var client = CreateFactoryWithTwoMails().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        var response = await client.DeleteAsync("/api/portal/mail-from-client-delete?id=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MailFromClientDelete_WithNonAdminUser_ReturnsForbidden()
    {
        var client = CreateFactoryWithTwoMails().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(2, "user@example.com", isAdmin: false));

        var response = await client.DeleteAsync("/api/portal/mail-from-client-delete?id=1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MailFromClientDelete_WithNonExistentId_ReturnsNotFound()
    {
        var client = CreateFactoryWithTwoMails().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.DeleteAsync("/api/portal/mail-from-client-delete?id=9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MailFromClientDelete_WithExistingId_ReturnsOk()
    {
        var client = CreateFactoryWithTwoMails().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.DeleteAsync("/api/portal/mail-from-client-delete?id=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task MailFromClientDelete_RemovesMailFromDatabase()
    {
        var factory = CreateFactoryWithTwoMails();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        await client.DeleteAsync("/api/portal/mail-from-client-delete?id=1");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deleted = await db.Mails.FindAsync(1L);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task MailFromClientDelete_DoesNotRemoveOtherMails()
    {
        var factory = CreateFactoryWithTwoMails();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        await client.DeleteAsync("/api/portal/mail-from-client-delete?id=1");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var remaining = await db.Mails.FindAsync(2L);
        Assert.NotNull(remaining);
        Assert.Equal("b@example.com", remaining.Email);
    }

    [Fact]
    public async Task MailFromClientDelete_DeletedMailReturnsNotFoundOnSecondDelete()
    {
        var factory = CreateFactoryWithTwoMails();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        await client.DeleteAsync("/api/portal/mail-from-client-delete?id=1");
        var secondResponse = await client.DeleteAsync("/api/portal/mail-from-client-delete?id=1");

        Assert.Equal(HttpStatusCode.NotFound, secondResponse.StatusCode);
    }
}
