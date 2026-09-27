using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;
using App01.Modules.Portal.Features.UserPassReset;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Portal.UserPassReset;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidXToken = "test-x-token";
    private const string JwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";
    private const string AdminEmail = "admin@example.com";
    private const string TargetEmail = "target@example.com";
    private const string TestPassword = "Password123";
    private const string DefaultResetPassword = "BrakBrak";

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
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private WebApplicationFactory<Program> CreateFactoryWithUsers()
    {
        return CreateFactoryWithData(db =>
        {
            db.Users.Add(new User
            {
                Id = 1,
                Email = AdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPassword),
                IsAdmin = true,
                CreatedAt = DateTime.UtcNow
            });
            db.Users.Add(new User
            {
                Id = 2,
                Email = TargetEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPassword),
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        });
    }

    [Fact]
    public async Task UserPassReset_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithUsers().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request(TargetEmail);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-pass-reset", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserPassReset_WithInvalidJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithUsers().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        var request = new Contracts.Request(TargetEmail);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-pass-reset", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserPassReset_WithNonAdminUser_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithUsers().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var token = GenerateJwtToken(2, TargetEmail, isAdmin: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new Contracts.Request(AdminEmail);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-pass-reset", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserPassReset_WithAdminUser_ReturnsOk()
    {
        // Arrange
        var client = CreateFactoryWithUsers().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new Contracts.Request(TargetEmail);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-pass-reset", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task UserPassReset_SetsPasswordToBrakBrak()
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new Contracts.Request(TargetEmail);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-pass-reset", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == TargetEmail);
        Assert.True(BCrypt.Net.BCrypt.Verify(DefaultResetPassword, user.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify(TestPassword, user.PasswordHash));
    }

    [Fact]
    public async Task UserPassReset_WithNonExistentEmail_ReturnsNotFound()
    {
        // Arrange
        var client = CreateFactoryWithUsers().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new Contracts.Request("notexist@example.com");

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-pass-reset", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("@nodomain")]
    public async Task UserPassReset_WithInvalidEmail_ReturnsBadRequest(string email)
    {
        // Arrange
        var client = CreateFactoryWithUsers().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new Contracts.Request(email);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-pass-reset", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserPassReset_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithUsers().CreateClient();
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new Contracts.Request(TargetEmail);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-pass-reset", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserPassReset_NonAdminDoesNotChangePassword()
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var token = GenerateJwtToken(2, TargetEmail, isAdmin: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new Contracts.Request(AdminEmail);

        // Act
        await client.PostAsJsonAsync("/api/portal/user-pass-reset", request);

        // Assert - haslo sie nie zmienilo
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == AdminEmail);
        Assert.True(BCrypt.Net.BCrypt.Verify(TestPassword, user.PasswordHash));
    }
}
