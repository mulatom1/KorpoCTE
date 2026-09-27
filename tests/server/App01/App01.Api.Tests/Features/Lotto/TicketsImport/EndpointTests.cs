using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

using App01.Modules.Lotto.Features.TicketsImport;
using App01.Shared.Application.Entities.Lotto;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Lotto.TicketsImport;

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
    public async Task ImportTickets_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);

        var request = new Contracts.Request(
            new List<Contracts.TicketDto> { new(1, "Group", new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>()) },
            null, null, null
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-import", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ImportTickets_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        var token = GenerateJwtToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new Contracts.Request(
            new List<Contracts.TicketDto> { new(1, "Group", new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>()) },
            null, null, null
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-import", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ImportTickets_WithTicketsList_ReturnsOk()
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

        var request = new Contracts.Request(
            new List<Contracts.TicketDto>
            {
                new(1, "Group1", new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>()),
                new(1, "Group2", new List<int> { 7, 8, 9, 10, 11, 12 }, new List<int>())
            },
            null, null, null
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-import", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ImportTickets_WithCsv_ReturnsOk()
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

        var csv = "1,2,3,4,5,6\n7,8,9,10,11,12";
        var request = new Contracts.Request(null, csv, 1, "ImportedGroup");

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-import", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(2, result.ImportedCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ImportTickets_WithCsvWithoutDrawTypeId_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var csv = "1,2,3,4,5,6";
        var request = new Contracts.Request(null, csv, null, null);

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-import", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportTickets_WithNoData_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var request = new Contracts.Request(null, null, null, null);

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-import", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportTickets_WithEmptyTicketsList_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var request = new Contracts.Request(new List<Contracts.TicketDto>(), null, null, null);

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-import", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportTickets_WithCsvWithSpecials_ReturnsOk()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 6, Name = "EuroJackpot", Description = "EuroJackpot game", TicketPrize = 12.5m, UserNumbersCountMin = 5, UserNumbersCountMax = 5, NumbersCount = 5, NumbersMaxValue = 50, SpecialsCount = 2, SpecialsMaxValue = 12 });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var csv = "10,20,30,40,50,S:5,S:10";
        var request = new Contracts.Request(null, csv, 6, "EuroGroup");

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-import", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(1, result.ImportedCount);
    }

    [Fact]
    public async Task ImportTickets_WithCsvAutoDetectSpecials_ReturnsOk()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.DrawTypes.Add(new DrawType { Id = 6, Name = "EuroJackpot", Description = "EuroJackpot game", TicketPrize = 12.5m, UserNumbersCountMin = 5, UserNumbersCountMax = 5, NumbersCount = 5, NumbersMaxValue = 50, SpecialsCount = 2, SpecialsMaxValue = 12 });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        // CSV without S: prefix - last 2 numbers should be auto-detected as specials
        var csv = "10,20,30,40,50,5,10";
        var request = new Contracts.Request(null, csv, 6, null);

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-import", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(1, result.ImportedCount);
    }

    [Fact]
    public async Task ImportTickets_WithCsvSemicolonSeparator_ReturnsOk()
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

        var csv = "1;2;3;4;5;6";
        var request = new Contracts.Request(null, csv, 1, null);

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-import", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(1, result.ImportedCount);
    }

    [Fact]
    public async Task ImportTickets_WithCsvSkipsHeaderLine_ReturnsOk()
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

        var csv = "DrawTypeId,GroupName,Number1,Number2,Number3,Number4,Number5,Number6\n1,2,3,4,5,6";
        var request = new Contracts.Request(null, csv, 1, null);

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-import", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(1, result.ImportedCount);
    }

    [Fact]
    public async Task ImportTickets_WithTicketsListInvalidDrawTypeId_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.Users.Add(new User { Id = 1, Email = "test@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken(userId: 1));

        var request = new Contracts.Request(
            new List<Contracts.TicketDto> { new(0, "Group", new List<int> { 1, 2, 3, 4, 5, 6 }, new List<int>()) },
            null, null, null
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-import", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportTickets_WithMultipleTickets_AllImported()
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

        var tickets = new List<Contracts.TicketDto>();
        for (int i = 0; i < 10; i++)
        {
            tickets.Add(new Contracts.TicketDto(1, $"Group{i}", new List<int> { i + 1, i + 2, i + 3, i + 4, i + 5, i + 6 }, new List<int>()));
        }

        var request = new Contracts.Request(tickets, null, null, null);

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/tickets-import", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(10, result.ImportedCount);
    }
}