using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

using App01.Modules.Portal.Features.MailFromClientList;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace App01.Bootstrapper.Api.Tests.Features.Portal.MailFromClientList;

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

    private WebApplicationFactory<Program> CreateFactoryWithMails()
    {
        return CreateFactoryWithData(db =>
        {
            for (int i = 1; i <= 15; i++)
            {
                db.Mails.Add(new Mail
                {
                    Id = i,
                    Email = $"user{i}@example.com",
                    Topic = $"Topic {i}",
                    Body = $"Body {i}",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-i)
                });
            }
            db.SaveChanges();
        });
    }

    [Fact]
    public async Task MailFromClientList_WithoutJwtToken_ReturnsUnauthorized()
    {
        var client = CreateFactoryWithMails().CreateClient();

        var response = await client.GetAsync("/api/portal/mail-from-client-list");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MailFromClientList_WithInvalidJwtToken_ReturnsUnauthorized()
    {
        var client = CreateFactoryWithMails().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        var response = await client.GetAsync("/api/portal/mail-from-client-list");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MailFromClientList_WithNonAdminUser_ReturnsForbidden()
    {
        var client = CreateFactoryWithMails().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(2, "user@example.com", isAdmin: false));

        var response = await client.GetAsync("/api/portal/mail-from-client-list");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MailFromClientList_WithAdminUser_ReturnsOkWithMails()
    {
        var client = CreateFactoryWithMails().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.GetAsync("/api/portal/mail-from-client-list");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(15, result.TotalCount);
    }

    [Fact]
    public async Task MailFromClientList_EmptyDatabase_ReturnsEmptyList()
    {
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.GetAsync("/api/portal/mail-from-client-list");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Empty(result.Mails);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task MailFromClientList_WithPagination_ReturnsCorrectPage()
    {
        var client = CreateFactoryWithMails().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.GetAsync("/api/portal/mail-from-client-list?page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(5, result.Mails.Count);
        Assert.Equal(15, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task MailFromClientList_SecondPage_ReturnsCorrectItems()
    {
        var client = CreateFactoryWithMails().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.GetAsync("/api/portal/mail-from-client-list?page=2&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(5, result.Mails.Count);
        Assert.Equal(2, result.Page);
    }

    [Fact]
    public async Task MailFromClientList_ReturnsMailsOrderedByCreatedAtDescending()
    {
        var client = CreateFactoryWithMails().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.GetAsync("/api/portal/mail-from-client-list");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        var dates = result.Mails.Select(m => m.CreatedAt).ToList();
        Assert.Equal(dates.OrderByDescending(d => d), dates);
    }

    [Fact]
    public async Task MailFromClientList_ReturnsCorrectMailFields()
    {
        var client = CreateFactoryWithData(db =>
        {
            db.Mails.Add(new Mail
            {
                Id = 1,
                Email = "check@example.com",
                Topic = "Check Topic",
                Body = "Check Body",
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.GetAsync("/api/portal/mail-from-client-list");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Mails);
        var mail = result.Mails.First();
        Assert.Equal("check@example.com", mail.Email);
        Assert.Equal("Check Topic", mail.Topic);
    }

    [Fact]
    public async Task MailFromClientList_DoesNotReturnBody()
    {
        var client = CreateFactoryWithData(db =>
        {
            db.Mails.Add(new Mail
            {
                Id = 1,
                Email = "x@example.com",
                Topic = "T",
                Body = "SecretBodyContent",
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.GetAsync("/api/portal/mail-from-client-list");

        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SecretBodyContent", content);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task MailFromClientList_WithInvalidPage_ReturnsBadRequest(int page)
    {
        var client = CreateFactoryWithMails().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.GetAsync($"/api/portal/mail-from-client-list?page={page}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task MailFromClientList_WithInvalidPageSize_ReturnsBadRequest(int pageSize)
    {
        var client = CreateFactoryWithMails().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.GetAsync($"/api/portal/mail-from-client-list?pageSize={pageSize}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MailFromClientList_DefaultPagination_ReturnsFirstPageWithDefaultSize()
    {
        var client = CreateFactoryWithMails().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin: true));

        var response = await client.GetAsync("/api/portal/mail-from-client-list");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(10, result.Mails.Count);
    }
}