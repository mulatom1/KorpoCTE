using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using App01.Shared.Application.Entities.Lotto;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;
using App01.Modules.Lotto.Features.TicketsDelete;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Lotto.TicketsDelete;

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
    public async Task DeleteTicket_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);

        // Act
        var response = await client.DeleteAsync("/api/lotto/tickets-delete?ticketId=1");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTicket_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        var token = GenerateJwtToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.DeleteAsync("/api/lotto/tickets-delete?ticketId=1");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTicket_WithValidData_ReturnsOk()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType {Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 5, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.DeleteAsync("/api/lotto/tickets-delete?ticketId=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task DeleteTicket_ValidationFails_WhenTicketIdIsZero()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.DeleteAsync("/api/lotto/tickets-delete?ticketId=0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTicket_ReturnsNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.DeleteAsync("/api/lotto/tickets-delete?ticketId=999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTicket_ReturnsForbidden_WhenTicketBelongsToAnotherUser()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.AddRange(
                new User { Id = 1, Email = "user1@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow },
                new User { Id = 2, Email = "user2@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow }
            );
            db.DrawTypes.Add(new DrawType {Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 2,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int>(),
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.DeleteAsync("/api/lotto/tickets-delete?ticketId=1");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTicket_DeletesTicket()
    {
        // Arrange
        var factory = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 5, 12, 23 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.DeleteAsync("/api/lotto/tickets-delete?ticketId=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify ticket is deleted
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await db.Tickets.FindAsync(1L);
        Assert.Null(ticket);
    }

    [Fact]
    public async Task DeleteTicket_DoesNotDeleteOtherTickets()
    {
        // Arrange
        var factory = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.Tickets.AddRange(
                new Ticket
                {
                    Id = 1,
                    UserId = 1,
                    DrawTypeId = 1,
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Ticket
                {
                    Id = 2,
                    UserId = 1,
                    DrawTypeId = 1,
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.DeleteAsync("/api/lotto/tickets-delete?ticketId=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var remainingTickets = await db.Tickets.ToListAsync();
        Assert.Single(remainingTickets);
        Assert.Equal(2, remainingTickets[0].Id);
    }
}
