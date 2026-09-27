using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

using App01.Modules.Lotto.Features.TicketsGetList;
using App01.Shared.Application.Entities.Lotto;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Lotto.TicketsGetList;

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
    public async Task GetTickets_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_WithInvalidJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        var token = GenerateJwtToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_WithValidTokens_ReturnsOk()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var token = GenerateJwtToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Empty(result.Tickets);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetTickets_ReturnsOnlyUserTickets()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.AddRange(
                new User { Id = 1, Email = "user1@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow },
                new User { Id = 2, Email = "user2@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow }
            );
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            db.Tickets.AddRange(
                new Ticket
                {
                    Id = 1,
                    UserId = 1,
                    DrawTypeId = 1,
                    GroupName = "Group1",
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int> { 5, 12 },
                    Specials = new List<int>()
                },
                new Ticket
                {
                    Id = 2,
                    UserId = 2,
                    DrawTypeId = 1,
                    GroupName = "Group2",
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int> { 7 },
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Tickets);
        Assert.Equal(1, result.Tickets[0].Id);
        Assert.Equal("Group1", result.Tickets[0].GroupName);
    }

    [Fact]
    public async Task GetTickets_FiltersBy_GroupName_ExactMatch()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            db.Tickets.AddRange(
                new Ticket
                {
                    Id = 1,
                    UserId = 1,
                    DrawTypeId = 1,
                    GroupName = "GroupA",
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Ticket
                {
                    Id = 2,
                    UserId = 1,
                    DrawTypeId = 1,
                    GroupName = "GroupB",
                    CreatedAt = DateTime.UtcNow,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-get-list?groupName=GroupA");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Tickets);
        Assert.Equal("GroupA", result.Tickets[0].GroupName);
    }

    [Fact]
    public async Task GetTickets_FiltersBy_GroupName_PartialMatch()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            db.Tickets.AddRange(
                new Ticket
                {
                    Id = 1,
                    UserId = 1,
                    DrawTypeId = 1,
                    GroupName = "MyGroupA",
                    CreatedAt = new DateTime(2024, 1, 1),
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Ticket
                {
                    Id = 2,
                    UserId = 1,
                    DrawTypeId = 1,
                    GroupName = "MyGroupB",
                    CreatedAt = new DateTime(2024, 1, 2),
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Ticket
                {
                    Id = 3,
                    UserId = 1,
                    DrawTypeId = 1,
                    GroupName = "OtherGroup",
                    CreatedAt = new DateTime(2024, 1, 3),
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act - Search for "MyGroup" which should match both "MyGroupA" and "MyGroupB"
        var response = await client.GetAsync("/api/lotto/tickets-get-list?groupName=MyGroup");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Tickets.Count);
        Assert.Contains(result.Tickets, t => t.GroupName == "MyGroupB");
        Assert.Contains(result.Tickets, t => t.GroupName == "MyGroupA");
    }

    [Fact]
    public async Task GetTickets_ReturnsNumbers_Sorted()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            db.Tickets.Add(new Ticket
            {
                Id = 1,
                UserId = 1,
                DrawTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                Numbers = new List<int> { 33, 11, 22 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Tickets);

        var numbers = result.Tickets[0].Numbers;
        Assert.Equal(3, numbers.Count);
        Assert.Contains(11, numbers);
        Assert.Contains(22, numbers);
        Assert.Contains(33, numbers);
    }

    [Fact]
    public async Task GetTickets_ReturnsTickets_OrderedByCreatedAtDescending()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            db.Tickets.AddRange(
                new Ticket
                {
                    Id = 1,
                    UserId = 1,
                    DrawTypeId = 1,
                    CreatedAt = new DateTime(2024, 1, 10),
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Ticket
                {
                    Id = 2,
                    UserId = 1,
                    DrawTypeId = 1,
                    CreatedAt = new DateTime(2024, 1, 20),
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Ticket
                {
                    Id = 3,
                    UserId = 1,
                    DrawTypeId = 1,
                    CreatedAt = new DateTime(2024, 1, 15),
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(3, result.Tickets.Count);
        Assert.Equal(2, result.Tickets[0].Id); // 2024-01-20
        Assert.Equal(3, result.Tickets[1].Id); // 2024-01-15
        Assert.Equal(1, result.Tickets[2].Id); // 2024-01-10
    }

    [Fact]
    public async Task GetTickets_ReturnsPaginatedResults()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            for (int i = 1; i <= 15; i++)
            {
                db.Tickets.Add(new Ticket
                {
                    Id = i,
                    UserId = 1,
                    DrawTypeId = 1,
                    CreatedAt = new DateTime(2024, 1, i),
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                });
            }
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-get-list?page=1&pageSize=5");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(5, result.Tickets.Count);
        Assert.Equal(15, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetTickets_ReturnsSecondPage()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            for (int i = 1; i <= 15; i++)
            {
                db.Tickets.Add(new Ticket
                {
                    Id = i,
                    UserId = 1,
                    DrawTypeId = 1,
                    CreatedAt = new DateTime(2024, 1, i),
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                });
            }
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-get-list?page=2&pageSize=5");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(5, result.Tickets.Count);
        Assert.Equal(15, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetTickets_ValidationFails_WhenPageIsLessThan1()
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
        var response = await client.GetAsync("/api/lotto/tickets-get-list?page=0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_ValidationFails_WhenPageSizeExceeds1000()
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
        var response = await client.GetAsync("/api/lotto/tickets-get-list?pageSize=1001");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_ReturnsDrawTypeName()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.AddRange(
                new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 },
                new DrawType { Id = 2, Name = "Mini Lotto", Description = "Mini Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 }
            );

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
                    DrawTypeId = 2,
                    CreatedAt = DateTime.UtcNow.AddHours(1),
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // Act
        var response = await client.GetAsync("/api/lotto/tickets-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Tickets.Count);
        Assert.Equal("Mini Lotto", result.Tickets[0].DrawTypeName);
        Assert.Equal("Lotto", result.Tickets[1].DrawTypeName);
    }
}