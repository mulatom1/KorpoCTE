using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

using App01.Modules.Lotto.Features.TicketsExport;
using App01.Shared.Application.Entities.Lotto;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Lotto.TicketsExport;

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
    public async Task ExportTickets_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-export");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExportTickets_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        var token = GenerateJwtToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-export");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ExportTickets_WithValidTokens_ReturnsOk()
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
        var response = await client.GetAsync("/api/lotto/tickets-export");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Empty(result.Tickets);
        Assert.Equal(0, result.TotalCount);
        Assert.NotEmpty(result.FileName);
        Assert.NotNull(result.Csv);
    }

    [Fact]
    public async Task ExportTickets_ReturnsOnlyUserTickets()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.AddRange(
                new User { Id = 1, Email = "user1@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow },
                new User { Id = 2, Email = "user2@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow }
            );
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 49, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Tickets.AddRange(
                new Ticket
                {
                    Id = 1,
                    UserId = 1,
                    DrawTypeId = 1,
                    GroupName = "Group1",
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                },
                new Ticket
                {
                    Id = 2,
                    UserId = 2,
                    DrawTypeId = 1,
                    GroupName = "Group2",
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int> { 7, 8, 9, 10, 11, 12 },
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-export");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Tickets);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Group1", result.Tickets[0].GroupName);
    }

    [Fact]
    public async Task ExportTickets_FiltersByGroupName()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 49, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Tickets.AddRange(
                new Ticket
                {
                    Id = 1,
                    UserId = 1,
                    DrawTypeId = 1,
                    GroupName = "GroupA",
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                },
                new Ticket
                {
                    Id = 2,
                    UserId = 1,
                    DrawTypeId = 1,
                    GroupName = "GroupB",
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int> { 7, 8, 9, 10, 11, 12 },
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-export?groupName=GroupA");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Tickets);
        Assert.Equal("GroupA", result.Tickets[0].GroupName);
    }

    [Fact]
    public async Task ExportTickets_FiltersByDrawTypeId()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.AddRange(
                new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 49, SpecialsCount = 0, SpecialsMaxValue = 0 },
                new DrawType { Id = 2, Name = "Mini Lotto", Description = "Mini Lotto game", TicketPrize = 2.0m, UserNumbersCountMin = 5, UserNumbersCountMax = 5, NumbersCount = 5, NumbersMaxValue = 42, SpecialsCount = 0, SpecialsMaxValue = 0 }
            );

            db.Tickets.AddRange(
                new Ticket
                {
                    Id = 1,
                    UserId = 1,
                    DrawTypeId = 1,
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                },
                new Ticket
                {
                    Id = 2,
                    UserId = 1,
                    DrawTypeId = 2,
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int> { 1, 2, 3, 4, 5 },
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-export?drawTypeId=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Tickets);
        Assert.Equal(1, result.Tickets[0].DrawTypeId);
    }

    [Fact]
    public async Task ExportTickets_ReturnsCsvWithCorrectFormat()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 49, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                GroupName = "TestGroup",
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-export");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Csv);
        Assert.Contains("1,TestGroup,1,2,3,4,5,6", result.Csv);
    }

    [Fact]
    public async Task ExportTickets_ReturnsCsvWithSpecials()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 6, Name = "EuroJackpot", Description = "EuroJackpot game", TicketPrize = 12.5m, UserNumbersCountMin = 5, UserNumbersCountMax = 5, NumbersCount = 5, NumbersMaxValue = 50, SpecialsCount = 2, SpecialsMaxValue = 12 });

            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 6,
                GroupName = "EuroGroup",
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 10, 20, 30, 40, 50 },
                Specials = new List<int> { 5, 10 }
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-export");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Csv);
        Assert.Contains("S:5", result.Csv);
        Assert.Contains("S:10", result.Csv);
    }

    [Fact]
    public async Task ExportTickets_ReturnsCorrectFileName()
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
        var response = await client.GetAsync("/api/lotto/tickets-export");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.StartsWith("Tickets_All_", result.FileName);
        Assert.EndsWith(".csv", result.FileName);
    }

    [Fact]
    public async Task ExportTickets_WithDrawTypeId_ReturnsFileNameWithDrawTypeId()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 49, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-export?drawTypeId=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.StartsWith("Tickets_01_", result.FileName);
    }
}