using System.Net;
using System.Net.Http.Json;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;
using App01.Modules.Portal.Features.UserPassChange;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace App01.Bootstrapper.Api.Tests.Features.Portal.UserPassChange;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidXToken = "test-x-token";
    private const string UserEmail = "user@example.com";
    private const string CurrentPassword = "CurrentPass123";
    private const string NewPassword = "NewPass456";

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

    private WebApplicationFactory<Program> CreateFactoryWithUser()
    {
        return CreateFactoryWithData(db =>
        {
            db.Users.Add(new User
            {
                Id = 1,
                Email = UserEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(CurrentPassword),
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        });
    }

    [Fact]
    public async Task UserPassChange_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithUser().CreateClient();
        var request = new Contracts.Request(UserEmail, CurrentPassword, NewPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-pass-change", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserPassChange_WithInvalidXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithUser().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "invalid-token");
        var request = new Contracts.Request(UserEmail, CurrentPassword, NewPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-pass-change", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserPassChange_WithValidData_ReturnsOk()
    {
        // Arrange
        var client = CreateFactoryWithUser().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request(UserEmail, CurrentPassword, NewPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-pass-change", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(UserEmail, result.Email);
    }

    [Fact]
    public async Task UserPassChange_ChangesPasswordInDatabase()
    {
        // Arrange
        var factory = CreateFactoryWithUser();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request(UserEmail, CurrentPassword, NewPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-pass-change", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == UserEmail);
        Assert.True(BCrypt.Net.BCrypt.Verify(NewPassword, user.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify(CurrentPassword, user.PasswordHash));
    }

    [Fact]
    public async Task UserPassChange_WithWrongCurrentPassword_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithUser().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request(UserEmail, "WrongPassword", NewPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-pass-change", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserPassChange_WithNonExistentEmail_ReturnsNotFound()
    {
        // Arrange
        var client = CreateFactoryWithUser().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request("notexist@example.com", CurrentPassword, NewPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-pass-change", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("", "CurrentPass123", "NewPass456")]
    [InlineData("not-an-email", "CurrentPass123", "NewPass456")]
    [InlineData("user@example.com", "", "NewPass456")]
    [InlineData("user@example.com", "CurrentPass123", "")]
    [InlineData("user@example.com", "CurrentPass123", "12345")] // nowe haslo za krotkie
    public async Task UserPassChange_WithInvalidData_ReturnsBadRequest(string login, string password1, string password2)
    {
        // Arrange
        var client = CreateFactoryWithUser().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request(login, password1, password2);

        // Act
        var response = await client.PostAsJsonAsync("/api/portal/user-pass-change", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserPassChange_WrongPassword_DoesNotChangeHashInDatabase()
    {
        // Arrange
        var factory = CreateFactoryWithUser();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var request = new Contracts.Request(UserEmail, "WrongPassword", NewPassword);

        // Act
        await client.PostAsJsonAsync("/api/portal/user-pass-change", request);

        // Assert - hash sie nie zmienil
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == UserEmail);
        Assert.True(BCrypt.Net.BCrypt.Verify(CurrentPassword, user.PasswordHash));
    }
}
