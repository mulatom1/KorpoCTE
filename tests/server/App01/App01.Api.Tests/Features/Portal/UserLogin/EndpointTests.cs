using System.Net;
using System.Net.Http.Json;

using App01.Modules.Portal.Features.UserLogin;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace App01.Bootstrapper.Api.Tests.Features.Portal.UserLogin;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidXToken = "test-x-token";
    private const string TestPassword = "Password123";
    private const string TestEmail = "testuser@example.com";

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
                    ["Jwt:Key"] = "ThisIsASecretKeyForTestingPurposesOnly123456",
                    ["Jwt:Issuer"] = "TestIssuer",
                    ["Jwt:Audience"] = "TestAudience",
                    ["Jwt:ExpiryInMinutes"] = "60",
                    ["Swagger:Enabled"] = "false",
                    ["Tokens:X-TOKEN"] = ValidXToken,
                    ["ConnectionStrings:DefaultConnection"] = "Server=dummy;Database=dummy;Integrated Security=True;"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Remove background workers to prevent them from running during tests
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

    private WebApplicationFactory<Program> CreateFactoryWithTestUser(bool isAdmin = false)
    {
        return CreateFactoryWithData(db =>
        {
            db.Users.Add(new User
            {
                Email = TestEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPassword),
                IsAdmin = isAdmin,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });
            db.SaveChanges();
        });
    }

    [Fact]
    public async Task UserLogin_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithTestUser().CreateClient();
        var request = new Contracts.Request(TestEmail, TestPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-login", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserLogin_WithInvalidXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithTestUser().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "invalid-token");
        var request = new Contracts.Request(TestEmail, TestPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-login", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserLogin_WithValidCredentials_ReturnsOkWithTokenAndUserData()
    {
        // Arrange
        var client = CreateFactoryWithTestUser().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request(TestEmail, TestPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-login", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(TestEmail, result.Email);
        Assert.NotEmpty(result.Token);
        Assert.True(result.TokenExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task UserLogin_WithAdminUser_ReturnsIsAdminTrue()
    {
        // Arrange
        var client = CreateFactoryWithTestUser(isAdmin: true).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request(TestEmail, TestPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-login", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.IsAdmin);
    }

    [Fact]
    public async Task UserLogin_WithNonAdminUser_ReturnsIsAdminFalse()
    {
        // Arrange
        var client = CreateFactoryWithTestUser(isAdmin: false).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request(TestEmail, TestPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-login", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.False(result.IsAdmin);
    }

    [Fact]
    public async Task UserLogin_WithNonExistentEmail_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithTestUser().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request("nonexistent@example.com", TestPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-login", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserLogin_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithTestUser().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request(TestEmail, "WrongPassword123");

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-login", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", "Password123")]
    [InlineData("invalid-email", "Password123")]
    [InlineData("test@example.com", "")]
    public async Task UserLogin_WithInvalidData_ReturnsBadRequest(string email, string password)
    {
        // Arrange
        var client = CreateFactoryWithTestUser().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request(email, password);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-login", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserLogin_WithEmptyEmail_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateFactoryWithTestUser().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request("", TestPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-login", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserLogin_WithInvalidEmailFormat_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateFactoryWithTestUser().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request("not-an-email", TestPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-login", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserLogin_WithEmptyPassword_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateFactoryWithTestUser().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request(TestEmail, "");

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-login", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserLogin_ReturnsValidJwtToken()
    {
        // Arrange
        var client = CreateFactoryWithTestUser().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request(TestEmail, TestPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-login", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);

        // JWT token format validation (header.payload.signature)
        var parts = result.Token.Split('.');
        Assert.Equal(3, parts.Length);
    }

    [Fact]
    public async Task UserLogin_TokenExpiresInFuture()
    {
        // Arrange
        var client = CreateFactoryWithTestUser().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request(TestEmail, TestPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-login", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.TokenExpiresAt > DateTime.UtcNow);
        Assert.True(result.TokenExpiresAt < DateTime.UtcNow.AddHours(2)); // Should be within reasonable time
    }

    [Fact]
    public async Task UserLogin_ReturnsCorrectUserCreatedAt()
    {
        // Arrange
        var createdAt = DateTime.UtcNow.AddDays(-5);
        var factory = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User
            {
                Email = "datetest@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPassword),
                IsAdmin = false,
                CreatedAt = createdAt
            });
            db.SaveChanges();
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request("datetest@example.com", TestPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-login", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(createdAt.Date, result.CreatedAt.Date);
    }
}