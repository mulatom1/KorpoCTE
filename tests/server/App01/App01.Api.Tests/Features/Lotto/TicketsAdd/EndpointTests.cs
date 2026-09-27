using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using App01.Shared.Application.Entities.Lotto;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;
using App01.Modules.Lotto.Features.TicketsAdd;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Lotto.TicketsAdd;

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

    private static string GenerateJwtToken(long userId = 1, string email = "test@example.com")
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
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

    [Fact]
    public async Task AddTicket_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);

        var request = new Contracts.Request(1, "TestGroup", new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddTicket_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        var token = GenerateJwtToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new Contracts.Request(1, "TestGroup", new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddTicket_WithValidData_ReturnsOk()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var request = new Contracts.Request(1, "TestGroup", new List<int> { 1, 12, 23, 34, 45, 49 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.True(result.CreatedAt <= DateTime.Now);
    }

    [Fact]
    public async Task AddTicket_WithNullGroupName_ReturnsOk()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType {Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var request = new Contracts.Request(1, null, new List<int> { 1, 12, 23, 34, 45, 49 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task AddTicket_With5Numbers_ReturnsOk()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 2, Name = "Mini Lotto", Description = "Mini Lotto game", TicketPrize = 2.0m, UserNumbersCountMin = 5, UserNumbersCountMax = 5, NumbersCount = 5, NumbersMaxValue = 42, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var request = new Contracts.Request(2, null, new List<int> { 1, 12, 23, 34, 42 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task AddTicket_ValidationFails_WhenDrawTypeIdIsZero()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var request = new Contracts.Request(0, "TestGroup", new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddTicket_ValidationFails_WhenNumbersEmpty()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var request = new Contracts.Request(1, "TestGroup", new List<int>(), new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddTicket_ValidationFails_WhenNumbersTooFew()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 6, NumbersMaxValue = 49, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var request = new Contracts.Request(1, "TestGroup", new List<int> { 1, 2, 3, 4 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddTicket_ValidationFails_WhenNumbersTooMany()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 6, NumbersMaxValue = 49, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var request = new Contracts.Request(1, "TestGroup", new List<int> { 1, 2, 3, 4, 5, 6, 7 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddTicket_ValidationFails_WhenNumberOutOfRange()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            // DrawType with NumbersMaxValue = 49 (so 50 will be out of range)
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 49, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var request = new Contracts.Request(1, "TestGroup", new List<int> { 1, 2, 3, 4, 5, 50 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddTicket_ValidationFails_WhenNumbersNotUnique()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var request = new Contracts.Request(1, "TestGroup", new List<int> { 1, 1, 3, 4, 5, 6 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddTicket_ReturnsNotFound_WhenDrawTypeDoesNotExist()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var request = new Contracts.Request(999, "TestGroup", new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddTicket_ReturnsForbidden_WhenMaxTicketsReached()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType {Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            // Add 500 tickets
            for (int i = 1; i <= 500; i++)
            {
                db.Tickets.Add(new Ticket
                {
                    Id = i,
                    UserId = 1,
                    DrawTypeId = 1,
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                });
            }
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var request = new Contracts.Request(1, "TestGroup", new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddTicket_NumbersAreStored()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var numbers = new List<int> { 49, 1, 25, 12, 33, 7 };
        var request = new Contracts.Request(1, null, numbers, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task AddTicket_ReturnsForbidden_WhenDuplicateNumbersExist()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Try to add ticket with same numbers
        var request = new Contracts.Request(1, "TestGroup", new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddTicket_ReturnsForbidden_WhenDuplicateNumbersInDifferentOrder()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Try to add ticket with same numbers but different order
        var request = new Contracts.Request(1, "TestGroup", new List<int> { 6, 5, 4, 3, 2, 1 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddTicket_ReturnsOk_WhenSameNumbersDifferentDrawType()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.AddRange(
                new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 },
                new DrawType { Id = 2, Name = "Lotto Plus", Description = "Lotto Plus game", TicketPrize = 4.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 60, SpecialsCount = 0, SpecialsMaxValue = 0 }
            );
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Add ticket with same numbers but different DrawType
        var request = new Contracts.Request(2, "TestGroup", new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AddTicket_ReturnsOk_WhenSameNumbersDifferentUser()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.AddRange(
                new User { Id = 1, Email = "user1@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow },
                new User { Id = 2, Email = "user2@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow }
            );
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });
            // User 2 has a ticket with numbers 1-6
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 2,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        // User 1 tries to add ticket with same numbers
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var request = new Contracts.Request(1, "TestGroup", new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>());

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-add", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

