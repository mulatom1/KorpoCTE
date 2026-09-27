using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using App01.Shared.Application.Entities.Lotto;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;
using App01.Modules.Lotto.Features.WinningTicketsList;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Lotto.WinningTicketsList;

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

                    // Seed DrawTypeWinTiers configuration data for all draw types
                    SeedDrawTypeWinTiers(db);

                    seedData(db);
                }
            });
        });
    }

    private static void SeedDrawTypeWinTiers(AppDbContext db)
    {
        // DrawType 1 (Lotto) - 6 numbers, no specials
        db.DrawTypeWinTiers.AddRange(
            new DrawTypeWinTier { Id = 1, DrawTypeId = 1, WinTier = 1, NumbersMatchCount = 6, NumbersMatchSelected = 6, SpecialsMatchCount = 0, SpecialsMatchSelected = 0, PotentialWinPrize = 2000000m },
            new DrawTypeWinTier { Id = 2, DrawTypeId = 1, WinTier = 2, NumbersMatchCount = 5, NumbersMatchSelected = 6, SpecialsMatchCount = 0, SpecialsMatchSelected = 0, PotentialWinPrize = 5000m },
            new DrawTypeWinTier { Id = 3, DrawTypeId = 1, WinTier = 3, NumbersMatchCount = 4, NumbersMatchSelected = 6, SpecialsMatchCount = 0, SpecialsMatchSelected = 0, PotentialWinPrize = 100m },
            new DrawTypeWinTier { Id = 4, DrawTypeId = 1, WinTier = 4, NumbersMatchCount = 3, NumbersMatchSelected = 6, SpecialsMatchCount = 0, SpecialsMatchSelected = 0, PotentialWinPrize = 24m }
        );

        // DrawType 2 (Lotto Plus) - 6 numbers, no specials
        db.DrawTypeWinTiers.AddRange(
            new DrawTypeWinTier { Id = 5, DrawTypeId = 2, WinTier = 1, NumbersMatchCount = 6, NumbersMatchSelected = 6, SpecialsMatchCount = 0, SpecialsMatchSelected = 0, PotentialWinPrize = 1000000m },
            new DrawTypeWinTier { Id = 6, DrawTypeId = 2, WinTier = 2, NumbersMatchCount = 5, NumbersMatchSelected = 6, SpecialsMatchCount = 0, SpecialsMatchSelected = 0, PotentialWinPrize = 2500m },
            new DrawTypeWinTier { Id = 7, DrawTypeId = 2, WinTier = 3, NumbersMatchCount = 4, NumbersMatchSelected = 6, SpecialsMatchCount = 0, SpecialsMatchSelected = 0, PotentialWinPrize = 50m },
            new DrawTypeWinTier { Id = 8, DrawTypeId = 2, WinTier = 4, NumbersMatchCount = 3, NumbersMatchSelected = 6, SpecialsMatchCount = 0, SpecialsMatchSelected = 0, PotentialWinPrize = 12m }
        );

        // DrawType 3 (MiniLotto) - 5 numbers, no specials
        db.DrawTypeWinTiers.AddRange(
            new DrawTypeWinTier { Id = 9, DrawTypeId = 3, WinTier = 1, NumbersMatchCount = 5, NumbersMatchSelected = 5, SpecialsMatchCount = 0, SpecialsMatchSelected = 0, PotentialWinPrize = 300000m },
            new DrawTypeWinTier { Id = 10, DrawTypeId = 3, WinTier = 2, NumbersMatchCount = 4, NumbersMatchSelected = 5, SpecialsMatchCount = 0, SpecialsMatchSelected = 0, PotentialWinPrize = 300m },
            new DrawTypeWinTier { Id = 11, DrawTypeId = 3, WinTier = 3, NumbersMatchCount = 3, NumbersMatchSelected = 5, SpecialsMatchCount = 0, SpecialsMatchSelected = 0, PotentialWinPrize = 10m }
        );

        // DrawType 6 (EuroJackpot) - 5 numbers, 2 specials
        db.DrawTypeWinTiers.AddRange(
            new DrawTypeWinTier { Id = 26, DrawTypeId = 6, WinTier = 1, NumbersMatchCount = 5, NumbersMatchSelected = 5, SpecialsMatchCount = 2, SpecialsMatchSelected = 2, PotentialWinPrize = 120000000m },
            new DrawTypeWinTier { Id = 27, DrawTypeId = 6, WinTier = 2, NumbersMatchCount = 5, NumbersMatchSelected = 5, SpecialsMatchCount = 1, SpecialsMatchSelected = 2, PotentialWinPrize = 500000m },
            new DrawTypeWinTier { Id = 28, DrawTypeId = 6, WinTier = 3, NumbersMatchCount = 5, NumbersMatchSelected = 5, SpecialsMatchCount = 0, SpecialsMatchSelected = 2, PotentialWinPrize = 100000m },
            new DrawTypeWinTier { Id = 29, DrawTypeId = 6, WinTier = 4, NumbersMatchCount = 4, NumbersMatchSelected = 5, SpecialsMatchCount = 2, SpecialsMatchSelected = 2, PotentialWinPrize = 5000m },
            new DrawTypeWinTier { Id = 30, DrawTypeId = 6, WinTier = 5, NumbersMatchCount = 4, NumbersMatchSelected = 5, SpecialsMatchCount = 1, SpecialsMatchSelected = 2, PotentialWinPrize = 200m },
            new DrawTypeWinTier { Id = 31, DrawTypeId = 6, WinTier = 6, NumbersMatchCount = 3, NumbersMatchSelected = 5, SpecialsMatchCount = 2, SpecialsMatchSelected = 2, PotentialWinPrize = 100m },
            new DrawTypeWinTier { Id = 32, DrawTypeId = 6, WinTier = 7, NumbersMatchCount = 4, NumbersMatchSelected = 5, SpecialsMatchCount = 0, SpecialsMatchSelected = 2, PotentialWinPrize = 50m },
            new DrawTypeWinTier { Id = 33, DrawTypeId = 6, WinTier = 8, NumbersMatchCount = 2, NumbersMatchSelected = 5, SpecialsMatchCount = 2, SpecialsMatchSelected = 2, PotentialWinPrize = 25m },
            new DrawTypeWinTier { Id = 34, DrawTypeId = 6, WinTier = 9, NumbersMatchCount = 3, NumbersMatchSelected = 5, SpecialsMatchCount = 1, SpecialsMatchSelected = 2, PotentialWinPrize = 20m },
            new DrawTypeWinTier { Id = 35, DrawTypeId = 6, WinTier = 10, NumbersMatchCount = 3, NumbersMatchSelected = 5, SpecialsMatchCount = 0, SpecialsMatchSelected = 2, PotentialWinPrize = 15m },
            new DrawTypeWinTier { Id = 36, DrawTypeId = 6, WinTier = 11, NumbersMatchCount = 1, NumbersMatchSelected = 5, SpecialsMatchCount = 2, SpecialsMatchSelected = 2, PotentialWinPrize = 10m },
            new DrawTypeWinTier { Id = 37, DrawTypeId = 6, WinTier = 12, NumbersMatchCount = 2, NumbersMatchSelected = 5, SpecialsMatchCount = 1, SpecialsMatchSelected = 2, PotentialWinPrize = 10m }
        );

        db.SaveChanges();
    }

    [Fact]
    public async Task WinningTicketsList_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WinningTicketsList_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        var token = GenerateJwtToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WinningTicketsList_WithValidTokens_ReturnsOk()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Empty(result.Draws);
        Assert.NotNull(result.Summary);
        Assert.Equal(0, result.Summary.TotalTickets);
        Assert.Equal(0, result.Summary.TotalDraws);
    }

    [Fact]
    public async Task WinningTicketsList_ReturnsMatchingTickets_WithMatchedNumbers()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            // Draw with numbers 1, 2, 3, 4, 5, 6
            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });

            // User ticket with numbers 1, 2, 3, 10, 11, 12 (3 matches = tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);

        var draw = result.Draws[0];
        Assert.Single(draw.MatchingTickets);

        var matchingTicket = draw.MatchingTickets[0];
        Assert.Equal(3, matchingTicket.MatchedNumbers.Count);
        Assert.Equal(4, matchingTicket.WinTier);
        Assert.Equal(24m, matchingTicket.WinPrize); // Tier 4 prize from seed data
        Assert.Contains(1, matchingTicket.MatchedNumbers);
        Assert.Contains(2, matchingTicket.MatchedNumbers);
        Assert.Contains(3, matchingTicket.MatchedNumbers);
    }

    [Fact]
    public async Task WinningTicketsList_FiltersBy_DrawDateFrom()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.AddRange(
                new Draw
                {
                    Id = 1,
                    DrawSystemId = 1001,
                    DrawDate = new DateTime(2024, 1, 10),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 2,
                    DrawSystemId = 1002,
                    DrawDate = new DateTime(2024, 1, 20),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                }
            );

            // Ticket with 3 matches (tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?drawDateFrom=2024-01-15");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);
        Assert.Equal(1002, result.Draws[0].DrawSystemId);
    }

    [Fact]
    public async Task WinningTicketsList_FiltersBy_DrawDateTo()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.AddRange(
                new Draw
                {
                    Id = 1,
                    DrawSystemId = 1001,
                    DrawDate = new DateTime(2024, 1, 10),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 2,
                    DrawSystemId = 1002,
                    DrawDate = new DateTime(2024, 1, 20),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                }
            );

            // Ticket with 3 matches (tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?drawDateTo=2024-01-15");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);
        Assert.Equal(1001, result.Draws[0].DrawSystemId);
    }

    [Fact]
    public async Task WinningTicketsList_FiltersBy_DrawTypeId()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.AddRange(
                new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 },
                new DrawType { Id = 2, Name = "LottoPlus", Description = "LottoPlus game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 }
            );

            db.Draws.AddRange(
                new Draw
                {
                    Id = 1,
                    DrawSystemId = 1001,
                    DrawDate = new DateTime(2024, 1, 15),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 2,
                    DrawSystemId = 2001,
                    DrawDate = new DateTime(2024, 1, 15),
                    DrawTypeId = 2,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                }
            );

            // Ticket for DrawType 2 with 3 matches (tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 2,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?drawTypeId=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);
        Assert.Equal(2001, result.Draws[0].DrawSystemId);
        Assert.Equal(2, result.Draws[0].DrawTypeId);
    }

    [Fact]
    public async Task WinningTicketsList_FiltersBy_GroupName()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });

            // Ticket with GroupName "GroupA" - 3 matches (tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                GroupName = "GroupA",
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });

            // Ticket with GroupName "MyOtherTickets" - 4 matches (tier 3)
            db.Tickets.Add(new Ticket
            {
                Id = 2,
                UserId = 1,
                DrawTypeId = 1,
                GroupName = "MyOtherTickets",
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 10, 11 },
                Specials = new List<int>()
            });

            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - filter by partial match "upA" (should match "GroupA" but not "MyOtherTickets")
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?groupName=upA");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);

        var draw = result.Draws[0];
        Assert.Single(draw.MatchingTickets);

        var ticket = draw.MatchingTickets[0];
        Assert.Equal("GroupA", ticket.GroupName);
        Assert.Equal(1, ticket.Id);
        Assert.Equal(4, ticket.WinTier); // 3 matches = tier 4
        Assert.Equal(3, ticket.MatchedNumbers.Count);
    }

    [Fact]
    public async Task WinningTicketsList_FiltersBy_GroupName_CalculatesCorrectSummary()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            // Two draws
            db.Draws.AddRange(
                new Draw
                {
                    Id = 1,
                    DrawSystemId = 1001,
                    DrawDate = new DateTime(2024, 1, 15),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 2,
                    DrawSystemId = 1002,
                    DrawDate = new DateTime(2024, 1, 16),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                }
            );

            // Two tickets in GroupA
            db.Tickets.AddRange(
                new Ticket
                {
                    Id = 1,
                    UserId = 1,
                    DrawTypeId = 1,
                    GroupName = "GroupA",
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                    Specials = new List<int>()
                },
                new Ticket
                {
                    Id = 2,
                    UserId = 1,
                    DrawTypeId = 1,
                    GroupName = "GroupA",
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int> { 1, 2, 3, 4, 10, 11 },
                    Specials = new List<int>()
                }
            );

            // One ticket in GroupB
            db.Tickets.Add(new Ticket
            {
                Id = 3,
                UserId = 1,
                DrawTypeId = 1,
                GroupName = "GroupB",
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 10, 11, 12, 13 },
                Specials = new List<int>()
            });

            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - filter by GroupA
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?groupName=GroupA");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        // Summary should only count GroupA tickets
        Assert.Equal(2, result.Summary.TotalDraws); // 2 draws
        Assert.Equal(2, result.Summary.TotalTickets); // 2 tickets in GroupA (not 3)
        Assert.Equal(4, result.Summary.TotalBets); // 2 tickets × 2 draws
        Assert.Equal(12.0m, result.Summary.TotalCost); // 2 tickets × 2 draws × 3.0 PLN
    }

    [Fact]
    public async Task WinningTicketsList_FiltersBy_GroupName_UsesLikePattern()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });

            // Three tickets with similar names
            db.Tickets.AddRange(
                new Ticket
                {
                    Id = 1,
                    UserId = 1,
                    DrawTypeId = 1,
                    GroupName = "MyGroup_Alpha",
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                    Specials = new List<int>()
                },
                new Ticket
                {
                    Id = 2,
                    UserId = 1,
                    DrawTypeId = 1,
                    GroupName = "MyGroup_Beta",
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int> { 1, 2, 3, 4, 10, 11 },
                    Specials = new List<int>()
                },
                new Ticket
                {
                    Id = 3,
                    UserId = 1,
                    DrawTypeId = 1,
                    GroupName = "OtherTickets",
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int> { 1, 2, 10, 11, 12, 13 },
                    Specials = new List<int>()
                }
            );

            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - filter by partial match "Group" (should match "MyGroup_Alpha" and "MyGroup_Beta" but not "OtherTickets")
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?groupName=Group");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);

        var draw = result.Draws[0];
        Assert.Equal(2, draw.MatchingTickets.Count); // Should match both MyGroup_Alpha and MyGroup_Beta

        var groupNames = draw.MatchingTickets.Select(t => t.GroupName).OrderBy(n => n).ToList();
        Assert.Equal("MyGroup_Alpha", groupNames[0]);
        Assert.Equal("MyGroup_Beta", groupNames[1]);

        // Summary should count both tickets
        Assert.Equal(2, result.Summary.TotalTickets);
    }

    [Fact]
    public async Task WinningTicketsList_ValidationFails_WhenDrawDateFromGreaterThanDrawDateTo()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?drawDateFrom=2024-01-20&drawDateTo=2024-01-10");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WinningTicketsList_ReturnsOnlyUserTickets()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.AddRange(
                new User { Id = 1, Email = "user1@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow },
                new User { Id = 2, Email = "user2@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow }
            );
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });

            // User 1 ticket
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });

            // User 2 ticket (should not appear)
            db.Tickets.Add(new Ticket
            {
                Id = 2,
                UserId = 2,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);
        Assert.Single(result.Draws[0].MatchingTickets);
        Assert.Equal(1, result.Draws[0].MatchingTickets[0].Id);
    }

    [Fact]
    public async Task WinningTicketsList_ReturnsSummary_WithCorrectTotals()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            // Two draws
            db.Draws.AddRange(
                new Draw
                {
                    Id = 1,
                    DrawSystemId = 1001,
                    DrawDate = new DateTime(2024, 1, 15),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 2,
                    DrawSystemId = 1002,
                    DrawDate = new DateTime(2024, 1, 20),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 1, 2, 3, 7, 8, 9 },
                    Specials = new List<int>()
                }
            );

            // Ticket with 3 matching numbers (tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        var summary = result.Summary;
        Assert.Equal(1, summary.TotalTickets);
        Assert.Equal(2, summary.TotalDraws);
        Assert.Single(summary.WinsByTier);
        Assert.Equal(4, summary.WinsByTier[0].WinTier);
        Assert.Equal(2, summary.WinsByTier[0].WinCount);
    }

    [Fact]
    public async Task WinningTicketsList_DrawType1_AllowsOnlyTiers1To4()
    {
        // Arrange - DrawType 1 should only allow tiers 1-4 (3+ matches for 6-number tickets)
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });

            // Ticket with only 2 matches (tier 5) - should NOT be included for DrawType 1
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 10, 11, 12, 13 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - HideDrawsWithoutMatches=true to get only draws with winning tickets
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?HideDrawsWithoutMatches=true");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Empty(result.Draws); // No draws returned because no winning tickets
    }

    [Fact]
    public async Task WinningTicketsList_DrawType3_AllowsOnlyTiers1To3()
    {
        // Arrange - DrawType 3 should only allow tiers 1-3 (3+ matches for 5-number tickets)
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 3, Name = "MiniLotto", Description = "Mini Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 3001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 3,
                Numbers = new List<int> { 1, 2, 3, 4, 5 },
                Specials = new List<int>()
            });

            // Ticket with only 2 matches (tier 4) - should NOT be included for DrawType 3
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 3,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - HideDrawsWithoutMatches=true to get only draws with winning tickets
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?HideDrawsWithoutMatches=true");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Empty(result.Draws); // No draws returned because no winning tickets
    }

    [Fact]
    public async Task WinningTicketsList_DrawType3_IncludesTier3()
    {
        // Arrange - DrawType 3 should include tier 3 (3 matches for 5-number tickets)
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 3, Name = "MiniLotto", Description = "Mini Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 3001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 3,
                Numbers = new List<int> { 1, 2, 3, 4, 5 },
                Specials = new List<int>()
            });

            // Ticket with 3 matches (tier 3) - SHOULD be included for DrawType 3
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 3,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);
        Assert.Single(result.Draws[0].MatchingTickets);
        Assert.Equal(3, result.Draws[0].MatchingTickets[0].MatchedNumbers.Count);
        Assert.Equal(3, result.Draws[0].MatchingTickets[0].WinTier);
    }

    [Fact]
    public async Task WinningTicketsList_NoMatchingTickets_ReturnsEmptyMatchingTickets()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int>(),
                Specials = new List<int>()
            });

            // Ticket with no matching numbers
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 10, 11, 12, 13, 14, 15 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - HideDrawsWithoutMatches=true to get only draws with winning tickets
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?HideDrawsWithoutMatches=true");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Empty(result.Draws); // No draws returned because no matching tickets
    }

    [Fact]
    public async Task WinningTicketsList_TicketMatchesOnlyItsDrawType()
    {
        // Arrange - Ticket DrawType 1 should not match Draw DrawType 2
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.AddRange(
                new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 },
                new DrawType { Id = 2, Name = "MiniLotto", Description = "MiniLotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 }
            );

            // Draw type 2
            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 2001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 2,
                Numbers = new List<int> { 1, 2, 3, 4, 5 },
                Specials = new List<int>()
            });

            // Ticket type 1 (different from draw type 2)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1, // Different type!
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - HideDrawsWithoutMatches=true to get only draws with winning tickets
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?HideDrawsWithoutMatches=true");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Empty(result.Draws); // No draws returned because ticket type doesn't match draw type
    }

    [Fact]
    public async Task WinningTicketsList_MultipleTicketsMultipleWinTiers()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });

            // Ticket 1: 5 matches (tier 2)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 10 },
                Specials = new List<int>()
            });

            // Ticket 2: 4 matches (tier 3)
            db.Tickets.Add(new Ticket
            {
                Id = 2,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 10, 11 },
                Specials = new List<int>()
            });

            // Ticket 3: 3 matches (tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 3,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);
        Assert.Equal(3, result.Draws[0].MatchingTickets.Count);

        var summary = result.Summary;
        Assert.Equal(3, summary.TotalTickets);
        Assert.Equal(1, summary.TotalDraws);
        Assert.Equal(3, summary.WinsByTier.Count);

        // Check tier 2
        var tier2 = summary.WinsByTier.First(t => t.WinTier == 2);
        Assert.Equal(1, tier2.WinCount);

        // Check tier 3
        var tier3 = summary.WinsByTier.First(t => t.WinTier == 3);
        Assert.Equal(1, tier3.WinCount);

        // Check tier 4
        var tier4 = summary.WinsByTier.First(t => t.WinTier == 4);
        Assert.Equal(1, tier4.WinCount);
    }

    [Fact]
    public async Task WinningTicketsList_ReturnsSummary_WithCorrectWinPrize()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });

            // Two tickets with 3 matches each (tier 4, 24 PLN each)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.Tickets.Add(new Ticket
            {
                Id = 2,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 4, 5, 6, 20, 21, 22 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        var summary = result.Summary;
        Assert.Single(summary.WinsByTier);

        var tier4 = summary.WinsByTier[0];
        Assert.Equal(4, tier4.WinTier);
        Assert.Equal(2, tier4.WinCount);
        Assert.Equal(48m, tier4.WinPrize); // 2 tickets * 24 PLN = 48 PLN

        // Also verify individual ticket prizes
        Assert.Equal(2, result.Draws[0].MatchingTickets.Count);
        Assert.All(result.Draws[0].MatchingTickets, ticket => Assert.Equal(24m, ticket.WinPrize));
    }

    [Fact]
    public async Task WinningTicketsList_SpecialNumbers_AreMatchedSeparately()
    {
        // Arrange - Test that special numbers are only compared with special numbers
        // and normal numbers are only compared with normal numbers
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 6, Name = "EuroJackpot", Description = "EuroJackpot game", TicketPrize = 12.5m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 5, NumbersMaxValue = 50, SpecialsCount = 2, SpecialsMaxValue = 12 });

            // Draw: normal numbers 1,2,3,4,5 and special numbers 1,2
            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 6001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 6,
                Numbers = new List<int> { 1, 2, 3, 4, 5 },
                Specials = new List<int> { 1, 2 }
            });

            // Ticket: normal 1,2,3,10,11 and special 1,5
            // Should match: 3 normal (1,2,3) and 1 special (1)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 6,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11 },
                Specials = new List<int> { 1, 5 }
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);
        Assert.Single(result.Draws[0].MatchingTickets);

        var matchingTicket = result.Draws[0].MatchingTickets[0];

        // Verify normal numbers matched correctly (only normal vs normal)
        Assert.Equal(3, matchingTicket.MatchedNumbers.Count);
        Assert.Contains(1, matchingTicket.MatchedNumbers);
        Assert.Contains(2, matchingTicket.MatchedNumbers);
        Assert.Contains(3, matchingTicket.MatchedNumbers);

        // Verify special numbers matched correctly (only special vs special)
        Assert.Single(matchingTicket.MatchedSpecials);
        Assert.Contains(1, matchingTicket.MatchedSpecials);
        // Special number 5 should NOT be matched (draw has special 1,2 not 5)
        Assert.DoesNotContain(5, matchingTicket.MatchedSpecials);
    }

    [Fact]
    public async Task WinningTicketsList_SpecialNumbers_OnlySpecialMatch_DoesNotIncludeTicket()
    {
        // Arrange - Test that ticket with only special number matches (no WinTier) is NOT included
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 6, Name = "EuroJackpot", Description = "EuroJackpot game", TicketPrize = 12.5m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 5, NumbersMaxValue = 50, SpecialsCount = 2, SpecialsMaxValue = 12 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 6001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 6,
                Numbers = new List<int> { 10, 20, 30, 40, 50 },
                Specials = new List<int> { 1, 2 }
            });

            // Ticket with NO normal matches but 2 special matches - no WinTier
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 6,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 5 },
                Specials = new List<int> { 1, 2 }
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - HideDrawsWithoutMatches=true to get only draws with winning tickets
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?HideDrawsWithoutMatches=true");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        // Ticket without WinTier is not included, so no draws are returned
        Assert.Empty(result.Draws);
    }

    [Fact]
    public async Task WinningTicketsList_Numbers_AreSorted()
    {
        // Arrange - Test that numbers are returned
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 6, Name = "EuroJackpot", Description = "EuroJackpot game", TicketPrize = 12.5m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 5, NumbersMaxValue = 50, SpecialsCount = 2, SpecialsMaxValue = 12 });

            // Add numbers in random order to verify sorting
            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 6001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 6,
                Numbers = new List<int> { 30, 10, 50, 20, 40 },
                Specials = new List<int> { 5, 2 }
            });

            // Ticket with some matching numbers to ensure it appears in results
            // Normal matches: 10, 20, 30 (3 matches) - enough to be included
            // Special match: 5 (1 match)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 6,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 25, 10, 45, 20, 30 },
                Specials = new List<int> { 5, 3 }
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        // Verify draw numbers are present
        var drawNumbers = result.Draws[0].Numbers;
        Assert.Equal(5, drawNumbers.Count);
        Assert.Contains(10, drawNumbers);
        Assert.Contains(20, drawNumbers);
        Assert.Contains(30, drawNumbers);
        Assert.Contains(40, drawNumbers);
        Assert.Contains(50, drawNumbers);

        // Verify special numbers are present
        var specialDrawNumbers = result.Draws[0].Specials;
        Assert.Equal(2, specialDrawNumbers.Count);
        Assert.Contains(2, specialDrawNumbers);
        Assert.Contains(5, specialDrawNumbers);

        // Verify ticket numbers are present
        Assert.Single(result.Draws[0].MatchingTickets);
        var ticketNumbers = result.Draws[0].MatchingTickets[0].Numbers;
        Assert.Equal(5, ticketNumbers.Count);
        Assert.Contains(10, ticketNumbers);
        Assert.Contains(20, ticketNumbers);
        Assert.Contains(25, ticketNumbers);
        Assert.Contains(30, ticketNumbers);
        Assert.Contains(45, ticketNumbers);

        var ticketSpecials = result.Draws[0].MatchingTickets[0].Specials;
        Assert.Equal(2, ticketSpecials.Count);
        Assert.Contains(3, ticketSpecials);
        Assert.Contains(5, ticketSpecials);
    }

    [Fact]
    public async Task WinningTicketsList_DefaultPagination_Returns100DrawsPerPage()
    {
        // Arrange - Pagination is now by DRAWS, not tickets
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            // Add 150 draws
            for (int i = 1; i <= 150; i++)
            {
                db.Draws.Add(new Draw
                {
                    Id = i,
                    DrawSystemId = 1000 + i,
                    DrawDate = new DateTime(2024, 1, 15).AddDays(-i),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                });
            }

            // Add 1 ticket with 3 matches (tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        // Default page size is 100 draws
        Assert.Equal(100, result.Draws.Count);
        Assert.Equal(150, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task WinningTicketsList_Pagination_ReturnsCorrectPage()
    {
        // Arrange - Pagination is now by DRAWS, not tickets
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            // Add 25 draws
            for (int i = 1; i <= 25; i++)
            {
                db.Draws.Add(new Draw
                {
                    Id = i,
                    DrawSystemId = 1000 + i,
                    DrawDate = new DateTime(2024, 1, 15).AddDays(-i),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                });
            }

            // Add 1 ticket with 3 matches (tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - Get page 2 with 10 draws per page
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?page=2&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        Assert.Equal(10, result.Draws.Count);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task WinningTicketsList_Pagination_LastPageHasRemainingItems()
    {
        // Arrange - Pagination is now by DRAWS, not tickets
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            // Add 25 draws
            for (int i = 1; i <= 25; i++)
            {
                db.Draws.Add(new Draw
                {
                    Id = i,
                    DrawSystemId = 1000 + i,
                    DrawDate = new DateTime(2024, 1, 15).AddDays(-i),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                });
            }

            // Add 1 ticket with 3 matches (tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - Get page 3 (last page) with 10 draws per page, should have 5 draws
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?page=3&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        Assert.Equal(5, result.Draws.Count);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(3, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task WinningTicketsList_Pagination_PageBeyondTotalReturnsEmpty()
    {
        // Arrange - Pagination is now by DRAWS, not tickets
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            // Add 5 draws
            for (int i = 1; i <= 5; i++)
            {
                db.Draws.Add(new Draw
                {
                    Id = i,
                    DrawSystemId = 1000 + i,
                    DrawDate = new DateTime(2024, 1, 15).AddDays(-i),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                });
            }

            // Add 1 ticket with 3 matches (tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - Request page 10 when only 1 page exists (5 draws with pageSize=10)
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?page=10&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        Assert.Empty(result.Draws);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(10, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task WinningTicketsList_Pagination_ValidationFails_WhenPageLessThan1()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?page=0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WinningTicketsList_Pagination_ValidationFails_WhenPageSizeLessThan1()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?pageSize=0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WinningTicketsList_Pagination_ValidationFails_WhenPageSizeExceeds1000()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?pageSize=1001");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WinningTicketsList_Pagination_SummaryIncludesAllTickets_NotJustCurrentPage()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });

            // Add 25 tickets with 3 matches (tier 4, 24 PLN each)
            for (int i = 1; i <= 25; i++)
            {
                db.Tickets.Add(new Ticket
                {
                    Id = i,
                    UserId = 1,
                    DrawTypeId = 1,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                    Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                    Specials = new List<int>()
                });
            }
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - Get only page 1 with 10 items
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?page=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        // Summary should include all 25 tickets, not just the 10 on current page
        var summary = result.Summary;
        Assert.Equal(25, summary.TotalTickets);
        Assert.Single(summary.WinsByTier);
        Assert.Equal(25, summary.WinsByTier[0].WinCount);
        Assert.Equal(600m, summary.WinsByTier[0].WinPrize); // 25 * 24 = 600
    }

    [Fact]
    public async Task WinningTicketsList_WinTierFilter_DefaultAllEnabled()
    {
        // Arrange - Create tickets with different win tiers
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });

            // Ticket 1: 5 matches (tier 2)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 10 },
                Specials = new List<int>()
            });

            // Ticket 2: 4 matches (tier 3)
            db.Tickets.Add(new Ticket
            {
                Id = 2,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 10, 11 },
                Specials = new List<int>()
            });

            // Ticket 3: 3 matches (tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 3,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - No WinTier filters specified, all should be enabled by default
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        // All 3 tickets should be returned
        var totalTicketsOnPage = result.Draws.Sum(d => d.MatchingTickets.Count);
        Assert.Equal(3, totalTicketsOnPage);
        Assert.Equal(3, result.Summary.WinsByTier.Count);
    }

    [Fact]
    public async Task WinningTicketsList_WinTierFilter_ExcludesTier4()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });

            // Ticket 1: 5 matches (tier 2)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 10 },
                Specials = new List<int>()
            });

            // Ticket 2: 3 matches (tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 2,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - Disable tier 4
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?winTier4=false");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        // Only tier 2 ticket should be returned
        var totalTicketsOnPage = result.Draws.Sum(d => d.MatchingTickets.Count);
        Assert.Equal(1, totalTicketsOnPage);
        Assert.Single(result.Summary.WinsByTier);
        Assert.Equal(2, result.Summary.WinsByTier[0].WinTier);
    }

    [Fact]
    public async Task WinningTicketsList_WinTierFilter_ExcludesMultipleTiers()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });

            // Ticket 1: 5 matches (tier 2)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 10 },
                Specials = new List<int>()
            });

            // Ticket 2: 4 matches (tier 3)
            db.Tickets.Add(new Ticket
            {
                Id = 2,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 10, 11 },
                Specials = new List<int>()
            });

            // Ticket 3: 3 matches (tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 3,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - Disable tier 3 and 4
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?winTier3=false&winTier4=false");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        // Only tier 2 ticket should be returned
        var totalTicketsOnPage = result.Draws.Sum(d => d.MatchingTickets.Count);
        Assert.Equal(1, totalTicketsOnPage);
        Assert.Single(result.Summary.WinsByTier);
        Assert.Equal(2, result.Summary.WinsByTier[0].WinTier);
    }

    [Fact]
    public async Task WinningTicketsList_WinTierFilter_OnlyTier4()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });

            // Ticket 1: 5 matches (tier 2)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 10 },
                Specials = new List<int>()
            });

            // Ticket 2: 4 matches (tier 3)
            db.Tickets.Add(new Ticket
            {
                Id = 2,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 10, 11 },
                Specials = new List<int>()
            });

            // Ticket 3: 3 matches (tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 3,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - Only enable tier 4
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?winTier1=false&winTier2=false&winTier3=false&winTier5=false&winTier6=false&winTier7=false&winTier8=false&winTier9=false&winTier10=false&winTier11=false&winTier12=false");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        // Only tier 4 ticket should be returned
        var totalTicketsOnPage = result.Draws.Sum(d => d.MatchingTickets.Count);
        Assert.Equal(1, totalTicketsOnPage);
        Assert.Single(result.Summary.WinsByTier);
        Assert.Equal(4, result.Summary.WinsByTier[0].WinTier);
    }

    [Fact]
    public async Task WinningTicketsList_WinTierFilter_SummaryCalculatedOnFilteredSet()
    {
        // Arrange - Pagination is now by DRAWS, not tickets
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });

            // Ticket 1: 5 matches (tier 2, 5000 PLN)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 10 },
                Specials = new List<int>()
            });

            // Tickets 2-11: 3 matches each (tier 4, 24 PLN each)
            for (int i = 2; i <= 11; i++)
            {
                db.Tickets.Add(new Ticket
                {
                    Id = i,
                    UserId = 1,
                    DrawTypeId = 1,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                    Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                    Specials = new List<int>()
                });
            }
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - Only show tier 4 (filter out tier 2)
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?winTier2=false");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        // 1 draw with 10 tier 4 tickets (tier 2 ticket filtered out)
        Assert.Single(result.Draws);
        var totalTicketsOnPage = result.Draws.Sum(d => d.MatchingTickets.Count);
        Assert.Equal(10, totalTicketsOnPage);

        // TotalCount is 1 (draws, not tickets)
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);

        // Summary should only include tier 4 wins (10 * 24 = 240)
        Assert.Single(result.Summary.WinsByTier);
        Assert.Equal(4, result.Summary.WinsByTier[0].WinTier);
        Assert.Equal(10, result.Summary.WinsByTier[0].WinCount);
        Assert.Equal(240m, result.Summary.WinsByTier[0].WinPrize);
    }

    [Fact]
    public async Task WinningTicketsList_WinTierFilter_AllDisabled_ReturnsEmpty()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });

            // Ticket with 3 matches (tier 4)
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - Disable all tiers and hide draws without matches
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?winTier1=false&winTier2=false&winTier3=false&winTier4=false&winTier5=false&winTier6=false&winTier7=false&winTier8=false&winTier9=false&winTier10=false&winTier11=false&winTier12=false&HideDrawsWithoutMatches=true");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        Assert.Empty(result.Draws);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Summary.WinsByTier);
    }

    [Fact]
    public async Task WinningTicketsList_Summary_CalculatesSimulationCorrectly()
    {
        // Arrange - Symulacja: 2 kupony Lotto × 2 losowania = 4 zakłady
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            // 2 losowania
            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 10),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });
            db.Draws.Add(new Draw
            {
                Id = 2,
                DrawSystemId = 1002,
                DrawDate = new DateTime(2024, 1, 20),
                DrawTypeId = 1,
                Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                Specials = new List<int>()
            });

            // 2 kupony (szablony) - jeden wygrywający, jeden przegrywający
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 1, 2, 3, 10, 11, 12 }, // 3 trafienia = tier 4
                Specials = new List<int>()
            });
            db.Tickets.Add(new Ticket
            {
                Id = 2,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 20, 21, 22, 23, 24, 25 }, // 0 trafień
                Specials = new List<int>()
            });

            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        var summary = result.Summary;

        // Podstawowe statystyki
        Assert.Equal(2, summary.TotalDraws);           // 2 losowania
        Assert.Equal(2, summary.TotalTickets);         // 2 szablony kuponów
        Assert.Equal(4, summary.TotalBets);            // 2 kupony × 2 losowania = 4 zakłady
        Assert.Equal(12.0m, summary.TotalCost);        // 4 zakłady × 3 PLN = 12 PLN

        // Wygrane (kupon 1 wygrywa w obu losowaniach)
        Assert.Equal(2, summary.TotalWinningBets);     // 2 wygrane zakłady
        Assert.Equal(48.0m, summary.TotalWinPrize);    // 2 × 24 PLN (tier 4) = 48 PLN

        // Bilans
        Assert.Equal(36.0m, summary.Balance);          // 48 - 12 = 36 PLN (zysk)
    }

    [Fact]
    public async Task WinningTicketsList_Summary_BalanceCanBeNegative()
    {
        // Arrange - Symulacja ze stratą
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            // 10 losowań
            for (int i = 1; i <= 10; i++)
            {
                db.Draws.Add(new Draw
                {
                    Id = i,
                    DrawSystemId = 1000 + i,
                    DrawDate = new DateTime(2024, 1, i),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 1, 2, 3, 4, 5, 6 },
                    Specials = new List<int>()
                });
            }

            // 1 kupon przegrywający
            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 20, 21, 22, 23, 24, 25 }, // 0 trafień
                Specials = new List<int>()
            });

            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        var summary = result.Summary;

        Assert.Equal(10, summary.TotalBets);           // 1 kupon × 10 losowań
        Assert.Equal(30.0m, summary.TotalCost);        // 10 × 3 PLN
        Assert.Equal(0, summary.TotalWinningBets);     // 0 wygranych
        Assert.Equal(0m, summary.TotalWinPrize);       // 0 PLN
        Assert.Equal(-30.0m, summary.Balance);         // 0 - 30 = -30 PLN (strata)
    }

    [Fact]
    public async Task WinningTicketsList_Summary_DateFiltersAffectCosts()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });

            // 2 losowania: 10 i 20 stycznia
            db.Draws.Add(new Draw { Id = 1, DrawSystemId = 1001, DrawDate = new DateTime(2024, 1, 10), DrawTypeId = 1, Numbers = new List<int> { 1, 2, 3, 4, 5, 6 }, Specials = new List<int>() });
            db.Draws.Add(new Draw { Id = 2, DrawSystemId = 1002, DrawDate = new DateTime(2024, 1, 20), DrawTypeId = 1, Numbers = new List<int> { 1, 2, 3, 4, 5, 6 }, Specials = new List<int>() });

            // 1 kupon
            db.Tickets.Add(new Ticket { Id = 1, UserId = 1, DrawTypeId = 1, CreatedAt = DateTime.UtcNow, Numbers = new List<int> { 1, 2, 3, 10, 11, 12 }, Specials = new List<int>() });

            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - Tylko 15-25 stycznia (1 losowanie w zakresie)
        var response = await client.GetAsync("/api/lotto/winning-tickets-list?drawDateFrom=2024-01-15&drawDateTo=2024-01-25");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        var summary = result.Summary;

        Assert.Equal(1, summary.TotalDraws);           // 1 losowanie w zakresie
        Assert.Equal(1, summary.TotalBets);            // 1 kupon × 1 losowanie
        Assert.Equal(3.0m, summary.TotalCost);         // 1 × 3 PLN
    }

    [Fact]
    public async Task WinningTicketsList_Summary_MultipleDrawTypes()
    {
        // Arrange - Symulacja z różnymi typami gier
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 });
            db.DrawTypes.Add(new DrawType { Id = 3, Name = "MiniLotto", Description = "MiniLotto", TicketPrize = 2.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 42, SpecialsCount = 0, SpecialsMaxValue = 0 });

            // 2 losowania Lotto
            db.Draws.Add(new Draw { Id = 1, DrawSystemId = 1001, DrawDate = new DateTime(2024, 1, 10), DrawTypeId = 1, Numbers = new List<int> { 1, 2, 3, 4, 5, 6 }, Specials = new List<int>() });
            db.Draws.Add(new Draw { Id = 2, DrawSystemId = 1002, DrawDate = new DateTime(2024, 1, 20), DrawTypeId = 1, Numbers = new List<int> { 1, 2, 3, 4, 5, 6 }, Specials = new List<int>() });

            // 3 losowania MiniLotto
            db.Draws.Add(new Draw { Id = 3, DrawSystemId = 3001, DrawDate = new DateTime(2024, 1, 5), DrawTypeId = 3, Numbers = new List<int> { 1, 2, 3, 4, 5 }, Specials = new List<int>() });
            db.Draws.Add(new Draw { Id = 4, DrawSystemId = 3002, DrawDate = new DateTime(2024, 1, 12), DrawTypeId = 3, Numbers = new List<int> { 1, 2, 3, 4, 5 }, Specials = new List<int>() });
            db.Draws.Add(new Draw { Id = 5, DrawSystemId = 3003, DrawDate = new DateTime(2024, 1, 19), DrawTypeId = 3, Numbers = new List<int> { 1, 2, 3, 4, 5 }, Specials = new List<int>() });

            // 1 kupon Lotto + 2 kupony MiniLotto
            db.Tickets.Add(new Ticket { Id = 1, UserId = 1, DrawTypeId = 1, CreatedAt = DateTime.UtcNow, Numbers = new List<int> { 20, 21, 22, 23, 24, 25 }, Specials = new List<int>() });
            db.Tickets.Add(new Ticket { Id = 2, UserId = 1, DrawTypeId = 3, CreatedAt = DateTime.UtcNow, Numbers = new List<int> { 20, 21, 22, 23, 24 }, Specials = new List<int>() });
            db.Tickets.Add(new Ticket { Id = 3, UserId = 1, DrawTypeId = 3, CreatedAt = DateTime.UtcNow, Numbers = new List<int> { 30, 31, 32, 33, 34 }, Specials = new List<int>() });

            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/winning-tickets-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);

        var summary = result.Summary;

        Assert.Equal(5, summary.TotalDraws);           // 2 Lotto + 3 MiniLotto
        Assert.Equal(3, summary.TotalTickets);         // 1 Lotto + 2 MiniLotto

        // TotalBets = (1 kupon Lotto × 2 losowania) + (2 kupony MiniLotto × 3 losowania) = 2 + 6 = 8
        Assert.Equal(8, summary.TotalBets);

        // TotalCost = (2 × 3 PLN) + (6 × 2 PLN) = 6 + 12 = 18 PLN
        Assert.Equal(18.0m, summary.TotalCost);
    }

    [Fact]
    public async Task WinningTicketsList_HideDrawsWithoutMatches_FiltersCorrectly()
    {
        // Arrange - 3 losowania: 2 z dopasowaniami, 1 bez
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 1, Name = "Lotto", Description = "Lotto", TicketPrize = 3.0m, UserNumbersCountMin = 6, UserNumbersCountMax = 6, NumbersCount = 6, NumbersMaxValue = 49, SpecialsCount = 0, SpecialsMaxValue = 0 });

            // Draw 1 - ma dopasowanie (3 trafienia = WinTier 4)
            db.Draws.Add(new Draw { Id = 1, DrawSystemId = 1001, DrawDate = new DateTime(2024, 1, 10), DrawTypeId = 1, Numbers = new List<int> { 1, 2, 3, 40, 41, 42 }, Specials = new List<int>() });
            // Draw 2 - brak dopasowania (inne numery)
            db.Draws.Add(new Draw { Id = 2, DrawSystemId = 1002, DrawDate = new DateTime(2024, 1, 15), DrawTypeId = 1, Numbers = new List<int> { 30, 31, 32, 33, 34, 35 }, Specials = new List<int>() });
            // Draw 3 - ma dopasowanie (3 trafienia = WinTier 4)
            db.Draws.Add(new Draw { Id = 3, DrawSystemId = 1003, DrawDate = new DateTime(2024, 1, 20), DrawTypeId = 1, Numbers = new List<int> { 1, 2, 3, 45, 46, 47 }, Specials = new List<int>() });

            // Kupon z numerami 1,2,3,4,5,6
            db.Tickets.Add(new Ticket { Id = 1, UserId = 1, DrawTypeId = 1, CreatedAt = DateTime.UtcNow, Numbers = new List<int> { 1, 2, 3, 4, 5, 6 }, Specials = new List<int>() });

            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act - bez filtra (powinny być wszystkie 3 losowania)
        var responseWithoutFilter = await client.GetAsync("/api/lotto/winning-tickets-list?hideDrawsWithoutMatches=false");
        Assert.Equal(HttpStatusCode.OK, responseWithoutFilter.StatusCode);
        var resultWithoutFilter = await responseWithoutFilter.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(resultWithoutFilter);
        Assert.Equal(3, resultWithoutFilter.TotalCount);
        Assert.Equal(3, resultWithoutFilter.Draws.Count);

        // Act - z filtrem (powinny być tylko 2 losowania z dopasowaniami)
        var responseWithFilter = await client.GetAsync("/api/lotto/winning-tickets-list?hideDrawsWithoutMatches=true");
        Assert.Equal(HttpStatusCode.OK, responseWithFilter.StatusCode);
        var resultWithFilter = await responseWithFilter.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(resultWithFilter);
        Assert.Equal(2, resultWithFilter.TotalCount);
        Assert.Equal(2, resultWithFilter.Draws.Count);

        // Sprawdź, że zwrócone losowania to te z dopasowaniami (Draw 1 i Draw 3)
        var drawIds = resultWithFilter.Draws.Select(d => d.Id).ToList();
        Assert.Contains(1L, drawIds);
        Assert.Contains(3L, drawIds);
        Assert.DoesNotContain(2L, drawIds);
    }
}
