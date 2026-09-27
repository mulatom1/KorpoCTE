using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using App01.Shared.Application.Entities.Lotto;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;
using App01.Modules.Lotto.Features.DrawsAdd;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Lotto.DrawsAdd;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidXToken = "test-x-token";
    private const string JwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";

    public EndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static string GenerateJwtToken(long userId = 1, string email = "admin@example.com", bool isAdmin = false)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (isAdmin)
        {
            claims.Add(new Claim("IsAdmin", "true"));
        }

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
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

    [Fact]
    public async Task AddDraw_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);

        var request = new Contracts.Request(1234, DateTime.UtcNow, 1, new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/draws-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddDraw_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        var token = GenerateJwtToken(isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new Contracts.Request(1234, DateTime.UtcNow, 1, new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/draws-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddDraw_WithNonAdminUser_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "user@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow, IsAdmin = false });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1, isAdmin: false));

        var request = new Contracts.Request(1234, DateTime.UtcNow, 1, new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/draws-add", request);

        // Assert - When user is not admin, GetIsAdminFromJwt may return Unauthorized
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddDraw_WithValidData_ReturnsOk()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "admin@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow, IsAdmin = true });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1, isAdmin: true));

        var drawDate = DateTime.UtcNow;
        var request = new Contracts.Request(1234, drawDate, 1, new List<int> { 1, 12, 23, 34, 45, 49 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/draws-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task AddDraw_ValidationFails_WhenDrawSystemIdIsZero()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "admin@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow, IsAdmin = true });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1, isAdmin: true));

        var request = new Contracts.Request(0, DateTime.UtcNow, 1, new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/draws-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddDraw_ValidationFails_WhenNumbersEmpty()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "admin@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow, IsAdmin = true });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1, isAdmin: true));

        var request = new Contracts.Request(1234, DateTime.UtcNow, 1, new List<int>(), new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/draws-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddDraw_ReturnsNotFound_WhenDrawTypeDoesNotExist()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "admin@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow, IsAdmin = true });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1, isAdmin: true));

        // Use DrawTypeId within valid range (1-10) but not seeded in DB
        var request = new Contracts.Request(1234, DateTime.UtcNow, 5, new List<int> { 1, 2, 3, 4, 5 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/draws-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddDraw_ReturnsForbidden_WhenDuplicateDrawSystemIdAndTypeExists()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "admin@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow, IsAdmin = true });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1234,
                DrawDate = DateTime.UtcNow,
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1, isAdmin: true));

        var request = new Contracts.Request(1234, DateTime.UtcNow, 1, new List<int> { 7, 8, 9, 10, 11, 12 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/draws-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddDraw_ReturnsOk_WhenSameDrawSystemIdButDifferentType()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "admin@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow, IsAdmin = true });
            db.DrawTypes.AddRange(
                new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 },
                new DrawType { Id = 2, Name = "Lotto Plus", Description = "Lotto Plus game", TicketPrize = 4.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 }
            );
            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1234,
                DrawDate = DateTime.UtcNow,
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1, isAdmin: true));

        var request = new Contracts.Request(1234, DateTime.UtcNow, 2, new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/draws-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
