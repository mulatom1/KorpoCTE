using System.Net;
using System.Net.Http.Json;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;
using App01.Modules.Portal.Features.UserRegister;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace App01.Bootstrapper.Api.Tests.Features.Portal.UserRegister;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
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
                    ["Jwt:Key"] = "ThisIsASecretKeyForTestingPurposesOnly123456",
                    ["Jwt:Issuer"] = "TestIssuer",
                    ["Jwt:Audience"] = "TestAudience",
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

    private static Contracts.Request CreateValidRequest() =>
        new("newuser@example.com", "Password123");

    [Fact]
    public async Task UserRegister_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        var request = CreateValidRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserRegister_WithInvalidXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "invalid-token");
        var request = CreateValidRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserRegister_WithValidData_ReturnsOkWithUserData()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = CreateValidRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-register", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("newuser@example.com", result.Email);
    }

    [Fact]
    public async Task UserRegister_SavesUserToDatabase()
    {
        // Arrange
        var factory = CreateFactoryWithData();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request("saved@example.com", "Password123");

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-register", request);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedUser = await db.Users.FindAsync(result.Id);

        Assert.NotNull(savedUser);
        Assert.Equal("saved@example.com", savedUser.Email);
        Assert.False(savedUser.IsAdmin);
        Assert.NotEmpty(savedUser.PasswordHash);
        Assert.NotEqual("Password123", savedUser.PasswordHash); // Password should be hashed
    }

    [Fact]
    public async Task UserRegister_WithExistingEmail_ReturnsBadRequest()
    {
        // Arrange
        var factory = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User
            {
                Email = "existing@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("ExistingPassword"),
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request("existing@example.com", "Password123");

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-register", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("", "Password123")]
    [InlineData("invalid-email", "Password123")]
    [InlineData("test@example.com", "")]
    [InlineData("test@example.com", "12345")] // Too short
    public async Task UserRegister_WithInvalidData_ReturnsBadRequest(string email, string password)
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request(email, password);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-register", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserRegister_WithEmptyEmail_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request("", "Password123");

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-register", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserRegister_WithInvalidEmailFormat_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request("not-an-email", "Password123");

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-register", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserRegister_WithShortPassword_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request("test@example.com", "12345");

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-register", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserRegister_PasswordIsHashedWithBCrypt()
    {
        // Arrange
        var factory = CreateFactoryWithData();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var password = "MySecurePassword123";
        var request = new Contracts.Request("hashtest@example.com", password);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-register", request);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedUser = await db.Users.FindAsync(result.Id);

        Assert.NotNull(savedUser);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, savedUser.PasswordHash));
    }

    [Fact]
    public async Task UserRegister_SetsIsAdminToFalse()
    {
        // Arrange
        var factory = CreateFactoryWithData();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request("admin@example.com", "Password123");

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-register", request);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedUser = await db.Users.FindAsync(result.Id);

        Assert.NotNull(savedUser);
        Assert.False(savedUser.IsAdmin);
    }
}
