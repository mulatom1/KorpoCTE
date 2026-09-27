using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;
using App01.Modules.Portal.Features.UsersRegister;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Portal.UsersRegister;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string JwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";
    private const string ValidXToken = "test-x-token";
    private const string Url = "/api/portal/users-register";

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

    private static string GenerateJwtToken(long userId, string email, bool isAdmin)
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

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> factory,
        bool isAdmin = true,
        bool withXToken = true,
        bool withJwt = true)
    {
        var client = factory.CreateClient();

        if (withXToken)
        {
            client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        }

        if (withJwt)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", GenerateJwtToken(1, "admin@example.com", isAdmin));
        }

        return client;
    }

    private static Contracts.Request CreateRequest(params string[] emails) =>
        new(emails.Select(e => new Contracts.UserToRegisterDto(e)).ToList());

    [Fact]
    public async Task UsersRegister_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var factory = CreateFactoryWithData();
        var client = CreateClient(factory, withXToken: false);

        // Act
        var response = await client.PostAsJsonAsync(Url, CreateRequest("a@example.com"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsersRegister_WithoutJwt_ReturnsUnauthorized()
    {
        // Arrange
        var factory = CreateFactoryWithData();
        var client = CreateClient(factory, withJwt: false);

        // Act
        var response = await client.PostAsJsonAsync(Url, CreateRequest("a@example.com"));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UsersRegister_AsNonAdmin_ReturnsForbidden()
    {
        // Arrange
        var factory = CreateFactoryWithData();
        var client = CreateClient(factory, isAdmin: false);

        // Act
        var response = await client.PostAsJsonAsync(Url, CreateRequest("a@example.com"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsersRegister_WithValidList_ReturnsIdsAndEmails()
    {
        // Arrange
        var factory = CreateFactoryWithData();
        var client = CreateClient(factory);
        var request = CreateRequest("student1@example.com", "student2@example.com", "student3@example.com");

        // Act
        var response = await client.PostAsJsonAsync(Url, request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(3, result.Users.Count);
        Assert.All(result.Users, u => Assert.True(u.Id > 0));
        Assert.Equal(
            new[] { "student1@example.com", "student2@example.com", "student3@example.com" },
            result.Users.Select(u => u.Email).ToArray());
    }

    [Fact]
    public async Task UsersRegister_SetsPasswordToStudentPlusIdWithExclamationMark()
    {
        // Arrange
        var factory = CreateFactoryWithData();
        var client = CreateClient(factory);
        var request = CreateRequest("pass1@example.com", "pass2@example.com");

        // Act
        var response = await client.PostAsJsonAsync(Url, request);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var registered in result.Users)
        {
            var savedUser = await db.Users.FindAsync(registered.Id);
            Assert.NotNull(savedUser);
            Assert.NotEmpty(savedUser.PasswordHash);
            Assert.True(
                BCrypt.Net.BCrypt.Verify($"Student{registered.Id}!", savedUser.PasswordHash),
                $"Password for user {registered.Id} should be Student{registered.Id}!");
        }
    }

    [Fact]
    public async Task UsersRegister_EachUserGetsDistinctPassword()
    {
        // Arrange
        var factory = CreateFactoryWithData();
        var client = CreateClient(factory);
        var request = CreateRequest("distinct1@example.com", "distinct2@example.com");

        // Act
        var response = await client.PostAsJsonAsync(Url, request);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();

        // Assert
        Assert.NotNull(result);
        var first = result.Users[0];
        var second = result.Users[1];
        Assert.NotEqual(first.Id, second.Id);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondUser = await db.Users.FindAsync(second.Id);

        Assert.NotNull(secondUser);
        Assert.False(BCrypt.Net.BCrypt.Verify($"Student{first.Id}!", secondUser.PasswordHash));
    }

    [Fact]
    public async Task UsersRegister_SavesUsersToDatabaseAsNonAdmins()
    {
        // Arrange
        var factory = CreateFactoryWithData();
        var client = CreateClient(factory);
        var request = CreateRequest("saved1@example.com", "saved2@example.com");

        // Act
        var response = await client.PostAsJsonAsync(Url, request);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var registered in result.Users)
        {
            var savedUser = await db.Users.FindAsync(registered.Id);
            Assert.NotNull(savedUser);
            Assert.Equal(registered.Email, savedUser.Email);
            Assert.False(savedUser.IsAdmin);
        }
    }

    [Fact]
    public async Task UsersRegister_TrimsEmails()
    {
        // Arrange
        var factory = CreateFactoryWithData();
        var client = CreateClient(factory);
        var request = CreateRequest("  trimmed@example.com  ");

        // Act
        var response = await client.PostAsJsonAsync(Url, request);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("trimmed@example.com", result.Users.Single().Email);
    }

    [Fact]
    public async Task UsersRegister_WithExistingEmail_ReturnsBadRequestAndSavesNothing()
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
        var client = CreateClient(factory);
        var request = CreateRequest("new@example.com", "existing@example.com");

        // Act
        var response = await client.PostAsJsonAsync(Url, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Email == "new@example.com"));
    }

    [Fact]
    public async Task UsersRegister_WithDuplicatedEmailsInRequest_ReturnsBadRequest()
    {
        // Arrange
        var factory = CreateFactoryWithData();
        var client = CreateClient(factory);
        var request = CreateRequest("dup@example.com", "DUP@example.com");

        // Act
        var response = await client.PostAsJsonAsync(Url, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UsersRegister_WithEmptyList_ReturnsBadRequest()
    {
        // Arrange
        var factory = CreateFactoryWithData();
        var client = CreateClient(factory);

        // Act
        var response = await client.PostAsJsonAsync(Url, CreateRequest());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task UsersRegister_WithInvalidEmail_ReturnsBadRequestAndSavesNothing(string invalidEmail)
    {
        // Arrange
        var factory = CreateFactoryWithData();
        var client = CreateClient(factory);
        var request = CreateRequest("valid@example.com", invalidEmail);

        // Act
        var response = await client.PostAsJsonAsync(Url, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Email == "valid@example.com"));
    }

    [Fact]
    public async Task UsersRegister_WithMoreThan100Users_ReturnsBadRequest()
    {
        // Arrange
        var factory = CreateFactoryWithData();
        var client = CreateClient(factory);
        var emails = Enumerable.Range(1, 101).Select(i => $"bulk{i}@example.com").ToArray();

        // Act
        var response = await client.PostAsJsonAsync(Url, CreateRequest(emails));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
