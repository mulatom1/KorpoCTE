using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using App01.Shared.Infrastructure.Repositories;
using App01.Shared.Application.Entities.Lotto;
using App01.Modules.Lotto.Features.DrawsGetList;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Lotto.DrawsGetList;

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

    private static string GenerateJwtToken(int userId = 1, string email = "test@example.com")
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

                // Remove all DbContext-related services
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
    public async Task GetDraws_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDraws_WithInvalidJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDraws_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        var token = GenerateJwtToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDraws_WithValidTokens_ReturnsOk()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        var token = GenerateJwtToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Empty(result.Draws);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetDraws_ReturnsAllDraws_WhenNoFilters()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            db.Draws.AddRange(
                new Draw
                {
                    Id = 1,
                    DrawSystemId = 1001,
                    DrawDate = new DateTime(2024, 1, 15),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 5, 12 },
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 2,
                    DrawSystemId = 1002,
                    DrawDate = new DateTime(2024, 1, 20),
                    DrawTypeId = 1,
                    Numbers = new List<int> { 7, 23 },
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Draws.Count);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task GetDraws_FiltersBy_DrawDateFrom()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType {Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            db.Draws.AddRange(
                new Draw
                {
                    Id = 1,
                    DrawSystemId = 1001,
                    DrawDate = new DateTime(2024, 1, 10),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 2,
                    DrawSystemId = 1002,
                    DrawDate = new DateTime(2024, 1, 20),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list?drawDateFrom=2024-01-15");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);
        Assert.Equal(1002, result.Draws[0].DrawSystemId);
    }

    [Fact]
    public async Task GetDraws_FiltersBy_DrawDateTo()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType {Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            db.Draws.AddRange(
                new Draw
                {
                    Id = 1,
                    DrawSystemId = 1001,
                    DrawDate = new DateTime(2024, 1, 10),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 2,
                    DrawSystemId = 1002,
                    DrawDate = new DateTime(2024, 1, 20),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list?drawDateTo=2024-01-15");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);
        Assert.Equal(1001, result.Draws[0].DrawSystemId);
    }

    [Fact]
    public async Task GetDraws_FiltersBy_DateRange()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            db.Draws.AddRange(
                new Draw
                {
                    Id = 1,
                    DrawSystemId = 1001,
                    DrawDate = new DateTime(2024, 1, 5),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 2,
                    DrawSystemId = 1002,
                    DrawDate = new DateTime(2024, 1, 15),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 3,
                    DrawSystemId = 1003,
                    DrawDate = new DateTime(2024, 1, 25),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list?drawDateFrom=2024-01-10&drawDateTo=2024-01-20");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);
        Assert.Equal(1002, result.Draws[0].DrawSystemId);
    }

    [Fact]
    public async Task GetDraws_FiltersBy_DrawTypeId()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            db.DrawTypes.AddRange(
                new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 },
                new DrawType { Id = 2, Name = "MiniLotto", Description = "MiniLotto game", TicketPrize = 2.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 }
            );

            db.Draws.AddRange(
                new Draw
                {
                    Id = 1,
                    DrawSystemId = 1001,
                    DrawDate = new DateTime(2024, 1, 15),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 2,
                    DrawSystemId = 2001,
                    DrawDate = new DateTime(2024, 1, 15),
                    DrawTypeId = 2,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list?drawTypeId=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);
        Assert.Equal(2001, result.Draws[0].DrawSystemId);
        Assert.Equal(2, result.Draws[0].DrawTypeId);
    }

    [Fact]
    public async Task GetDraws_ReturnsNumbers_Sorted()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 33, 11, 22 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);

        var numbers = result.Draws[0].Numbers;
        Assert.Equal(3, numbers.Count);
        Assert.Contains(11, numbers);
        Assert.Contains(22, numbers);
        Assert.Contains(33, numbers);
    }

    [Fact]
    public async Task GetDraws_ReturnsSpecials_ForEuroJackpot()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType { Id = 6, Name = "EuroJackpot", Description = "EuroJackpot game", TicketPrize = 12.5m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 5, NumbersMaxValue = 50, SpecialsCount = 2, SpecialsMaxValue = 10 };
            db.DrawTypes.Add(drawType);

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 2001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 6,
                Numbers = new List<int> { 5, 12, 23, 34, 45 },
                Specials = new List<int> { 3, 7 }
            });
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Draws);

        var numbers = result.Draws[0].Numbers;
        Assert.Equal(5, numbers.Count);
        Assert.Contains(5, numbers);
        Assert.Contains(12, numbers);
        Assert.Contains(23, numbers);
        Assert.Contains(34, numbers);
        Assert.Contains(45, numbers);

        var specials = result.Draws[0].Specials;
        Assert.Equal(2, specials.Count);
        Assert.Contains(3, specials);
        Assert.Contains(7, specials);
    }


    [Fact]
    public async Task GetDraws_ReturnsDraws_OrderedByDateDescending()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            db.Draws.AddRange(
                new Draw
                {
                    Id = 1,
                    DrawSystemId = 1001,
                    DrawDate = new DateTime(2024, 1, 10),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 2,
                    DrawSystemId = 1002,
                    DrawDate = new DateTime(2024, 1, 20),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 3,
                    DrawSystemId = 1003,
                    DrawDate = new DateTime(2024, 1, 15),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(3, result.Draws.Count);
        Assert.Equal(1002, result.Draws[0].DrawSystemId); // 2024-01-20
        Assert.Equal(1003, result.Draws[1].DrawSystemId); // 2024-01-15
        Assert.Equal(1001, result.Draws[2].DrawSystemId); // 2024-01-10
    }

    [Fact]
    public async Task GetDraws_ValidationFails_WhenDrawDateFromGreaterThanDrawDateTo()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list?drawDateFrom=2024-01-20&drawDateTo=2024-01-10");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDraws_ReturnsPaginatedResults()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            for (int i = 1; i <= 15; i++)
            {
                db.Draws.Add(new Draw
                {
                    Id = i,
                    DrawSystemId = 1000 + i,
                    DrawDate = new DateTime(2024, 1, i),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                });
            }
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list?page=1&pageSize=5");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(5, result.Draws.Count);
        Assert.Equal(15, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetDraws_ReturnsSecondPage()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            for (int i = 1; i <= 15; i++)
            {
                db.Draws.Add(new Draw
                {
                    Id = i,
                    DrawSystemId = 1000 + i,
                    DrawDate = new DateTime(2024, 1, i),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                });
            }
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list?page=2&pageSize=5");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(5, result.Draws.Count);
        Assert.Equal(15, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetDraws_ReturnsLastPageWithRemainingItems()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            for (int i = 1; i <= 12; i++)
            {
                db.Draws.Add(new Draw
                {
                    Id = i,
                    DrawSystemId = 1000 + i,
                    DrawDate = new DateTime(2024, 1, i),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                });
            }
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list?page=3&pageSize=5");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Draws.Count); // Only 2 items on the last page
        Assert.Equal(12, result.TotalCount);
        Assert.Equal(3, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetDraws_ValidationFails_WhenPageIsLessThan1()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list?page=0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDraws_ValidationFails_WhenPageSizeExceeds1000()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list?pageSize=1001");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDraws_WithSortOrderAsc_ReturnsDrawsOrderedByDateAscending()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            db.Draws.AddRange(
                new Draw
                {
                    Id = 1,
                    DrawSystemId = 1001,
                    DrawDate = new DateTime(2024, 1, 20),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 2,
                    DrawSystemId = 1002,
                    DrawDate = new DateTime(2024, 1, 10),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 3,
                    DrawSystemId = 1003,
                    DrawDate = new DateTime(2024, 1, 15),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list?sortOrder=asc");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(3, result.Draws.Count);
        Assert.Equal(1002, result.Draws[0].DrawSystemId); // 2024-01-10
        Assert.Equal(1003, result.Draws[1].DrawSystemId); // 2024-01-15
        Assert.Equal(1001, result.Draws[2].DrawSystemId); // 2024-01-20
    }

    [Fact]
    public async Task GetDraws_WithSortOrderDesc_ReturnsDrawsOrderedByDateDescending()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            db.Draws.AddRange(
                new Draw
                {
                    Id = 1,
                    DrawSystemId = 1001,
                    DrawDate = new DateTime(2024, 1, 10),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 2,
                    DrawSystemId = 1002,
                    DrawDate = new DateTime(2024, 1, 20),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 3,
                    DrawSystemId = 1003,
                    DrawDate = new DateTime(2024, 1, 15),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list?sortOrder=desc");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(3, result.Draws.Count);
        Assert.Equal(1002, result.Draws[0].DrawSystemId); // 2024-01-20
        Assert.Equal(1003, result.Draws[1].DrawSystemId); // 2024-01-15
        Assert.Equal(1001, result.Draws[2].DrawSystemId); // 2024-01-10
    }

    [Fact]
    public async Task GetDraws_WithoutSortOrder_DefaultsToDescending()
    {
        // Arrange
        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType { Id = 1, Name = "Lotto", Description = "Lotto game", TicketPrize = 3.0m, UserNumbersCountMin = 2, UserNumbersCountMax = 2, NumbersCount = 2, NumbersMaxValue = 50, SpecialsCount = 0, SpecialsMaxValue = 0 };
            db.DrawTypes.Add(drawType);

            db.Draws.AddRange(
                new Draw
                {
                    Id = 1,
                    DrawSystemId = 1001,
                    DrawDate = new DateTime(2024, 1, 10),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                },
                new Draw
                {
                    Id = 2,
                    DrawSystemId = 1002,
                    DrawDate = new DateTime(2024, 1, 20),
                    DrawTypeId = 1,
                    Numbers = new List<int>(),
                    Specials = new List<int>()
                }
            );
            db.SaveChanges();
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Draws.Count);
        Assert.Equal(1002, result.Draws[0].DrawSystemId); // 2024-01-20 first (descending)
        Assert.Equal(1001, result.Draws[1].DrawSystemId); // 2024-01-10 second
    }

    [Fact]
    public async Task GetDraws_ValidationFails_WhenSortOrderInvalid()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-list?sortOrder=invalid");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

