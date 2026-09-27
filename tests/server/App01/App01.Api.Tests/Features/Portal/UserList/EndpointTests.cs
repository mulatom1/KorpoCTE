using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

using App01.Modules.Portal.Features.UserList;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Portal.UserList;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string JwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";
    private const string AdminEmail = "admin@example.com";
    private const string TestPassword = "Password123";

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
                    ["Tokens:X-TOKEN"] = "test-x-token",
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
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            });
            db.Users.Add(new User
            {
                Id = 2,
                Email = "user1@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPassword),
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            });
            db.Users.Add(new User
            {
                Id = 3,
                Email = "user2@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPassword),
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            });
            db.Users.Add(new User
            {
                Id = 4,
                Email = "admin2@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPassword),
                IsAdmin = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });
            db.SaveChanges();
        });
    }

    [Fact]
    public async Task UserList_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithUsers().CreateClient();

        // Act
        var response = await client.GetAsync("/api/portal/user-list");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserList_WithInvalidJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithUsers().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await client.GetAsync("/api/portal/user-list");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserList_WithNonAdminUser_ReturnsForbidden()
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "test-x-token");
        var token = GenerateJwtToken(2, "user1@example.com", isAdmin: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/portal/user-list");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserList_WithAdminUser_ReturnsOkWithUsers()
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "test-x-token");
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/portal/user-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(4, result.Users.Count);
    }

    [Fact]
    public async Task UserList_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "test-x-token");
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/portal/user-list?page=1&pageSize=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(2, result.Users.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task UserList_WithPagination_ReturnsSecondPage()
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "test-x-token");
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/portal/user-list?page=2&pageSize=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(2, result.Users.Count);
        Assert.Equal(2, result.Page);
    }

    [Fact]
    public async Task UserList_FilterByEmail_ReturnsMatchingUsers()
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "test-x-token");
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/portal/user-list?email=admin");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Users, u => Assert.Contains("admin", u.Email));
    }

    [Fact]
    public async Task UserList_FilterByIsAdminTrue_ReturnsOnlyAdmins()
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "test-x-token");
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/portal/user-list?isAdmin=true");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Users, u => Assert.True(u.IsAdmin));
    }

    [Fact]
    public async Task UserList_FilterByIsAdminFalse_ReturnsOnlyNonAdmins()
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "test-x-token");
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/portal/user-list?isAdmin=false");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Users, u => Assert.False(u.IsAdmin));
    }

    [Fact]
    public async Task UserList_CombinedFilters_ReturnsCorrectResults()
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "test-x-token");
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/portal/user-list?email=user&isAdmin=false");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Users, u =>
        {
            Assert.Contains("user", u.Email);
            Assert.False(u.IsAdmin);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UserList_WithInvalidPage_ReturnsBadRequest(int page)
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "test-x-token");
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync($"/api/portal/user-list?page={page}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task UserList_WithInvalidPageSize_ReturnsBadRequest(int pageSize)
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "test-x-token");
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync($"/api/portal/user-list?pageSize={pageSize}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserList_ReturnsUsersOrderedByEmail()
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "test-x-token");
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/portal/user-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        var emails = result.Users.Select(u => u.Email).ToList();
        Assert.Equal(emails.OrderBy(e => e), emails);
    }

    [Fact]
    public async Task UserList_ReturnsCorrectUserData()
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "test-x-token");
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/portal/user-list?email=admin@example.com");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Users);
        var user = result.Users.First();
        Assert.Equal(1, user.Id);
        Assert.Equal(AdminEmail, user.Email);
        Assert.True(user.IsAdmin);
    }

    [Fact]
    public async Task UserList_WithNoMatchingFilter_ReturnsEmptyList()
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "test-x-token");
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/portal/user-list?email=nonexistent");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Empty(result.Users);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task UserList_DefaultPagination_ReturnsFirstPageWithDefaultSize()
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "test-x-token");
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/portal/user-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task UserList_DoesNotExposePasswordHash()
    {
        // Arrange
        var factory = CreateFactoryWithUsers();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", "test-x-token");
        var token = GenerateJwtToken(1, AdminEmail, isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/portal/user-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", content, StringComparison.OrdinalIgnoreCase);
    }
}