using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using App01.Shared.Infrastructure.Repositories;
using App01.Shared.Application.Entities.Lotto;
using App01.Modules.Lotto.Features.DrawsGetPrizesList;
using App01.Modules.Lotto.Services.LottoOpenApi;
using App01.Modules.Lotto.Services.LottoOpenApi.Dto;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Moq;


namespace App01.Bootstrapper.Api.Tests.Features.Lotto.DrawsGetPrizesList;

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

    private WebApplicationFactory<Program> CreateFactoryWithData(
        Action<AppDbContext>? seedData = null,
        Mock<ILottoOpenApiService>? lottoOpenApiServiceMock = null)
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

                // Remove existing ILottoOpenApiService registration and add mock
                if (lottoOpenApiServiceMock != null)
                {
                    var lottoServiceDescriptor = services
                        .FirstOrDefault(d => d.ServiceType == typeof(ILottoOpenApiService));
                    if (lottoServiceDescriptor != null)
                    {
                        services.Remove(lottoServiceDescriptor);
                    }
                    services.AddSingleton(lottoOpenApiServiceMock.Object);
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
    public async Task GetPrizesList_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-prizes-list?drawTypeId=1&drawSystemId=1001");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPrizesList_WithInvalidJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-prizes-list?drawTypeId=1&drawSystemId=1001");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPrizesList_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        var token = GenerateJwtToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-prizes-list?drawTypeId=1&drawSystemId=1001");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPrizesList_ValidationFails_WhenDrawTypeIdIsZero()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-prizes-list?drawTypeId=0&drawSystemId=1001");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPrizesList_ValidationFails_WhenDrawSystemIdIsZero()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-prizes-list?drawTypeId=1&drawSystemId=0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPrizesList_ValidationFails_WhenDrawTypeIdIsNegative()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-prizes-list?drawTypeId=-1&drawSystemId=1001");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPrizesList_ReturnsInternalServerError_WhenDrawTypeDoesNotExist()
    {
        // Arrange
        // Note: Handler throws KeyNotFoundException which is not mapped in middleware,
        // resulting in 500 InternalServerError instead of 404 NotFound
        var mockLottoService = new Mock<ILottoOpenApiService>();
        var client = CreateFactoryWithData(lottoOpenApiServiceMock: mockLottoService).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-prizes-list?drawTypeId=999&drawSystemId=1001");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task GetPrizesList_ReturnsInternalServerError_WhenDrawDoesNotExist()
    {
        // Arrange
        // Note: Handler throws KeyNotFoundException which is not mapped in middleware,
        // resulting in 500 InternalServerError instead of 404 NotFound
        var mockLottoService = new Mock<ILottoOpenApiService>();
        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType
            {
                Id = 1,
                Name = "Lotto",
                Description = "Lotto",
                TicketPrize = 3.0m,
                UserNumbersCountMin = 2,
                UserNumbersCountMax = 2,
                NumbersCount = 6,
                NumbersMaxValue = 49,
                SpecialsCount = 0,
                SpecialsMaxValue = 0
            };
            db.DrawTypes.Add(drawType);
            db.SaveChanges();
        }, mockLottoService).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-prizes-list?drawTypeId=1&drawSystemId=9999");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task GetPrizesList_ReturnsEmptyPrizes_WhenApiReturnsNull()
    {
        // Arrange
        var mockLottoService = new Mock<ILottoOpenApiService>();
        mockLottoService.Setup(x => x.GetDrawPrizes(It.IsAny<string>(), It.IsAny<long>()))
            .ReturnsAsync((List<DrawStatsResponse>?)null);

        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType
            {
                Id = 1,
                Name = "Lotto",
                Description = "Lotto",
                TicketPrize = 3.0m,
                UserNumbersCountMin = 2,
                UserNumbersCountMax = 2,
                NumbersCount = 6,
                NumbersMaxValue = 49,
                SpecialsCount = 0,
                SpecialsMaxValue = 0
            };
            db.DrawTypes.Add(drawType);

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 5, 12, 23, 34, 45, 49 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }, mockLottoService).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-prizes-list?drawTypeId=1&drawSystemId=1001");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Empty(result.WinTiers);
        Assert.Equal(1001, result.DrawSystemId);
        Assert.Equal(new DateTime(2024, 1, 15), result.DrawDate);
        Assert.Equal("Lotto", result.GameType);
    }

    [Fact]
    public async Task GetPrizesList_ReturnsPrizes_WhenApiReturnsPrizes()
    {
        // Arrange
        var mockLottoService = new Mock<ILottoOpenApiService>();
        var apiResponse = new List<DrawStatsResponse>
        {
            new DrawStatsResponse
            {
                DrawSystemId = 1001,
                GameType = "Lotto",
                Prizes = new Dictionary<string, DrawStatsResponsePrizeInfo>
                {
                    ["1"] = new DrawStatsResponsePrizeInfo { Prize = 1, PrizeValue = 1000000.00m },
                    ["2"] = new DrawStatsResponsePrizeInfo { Prize = 5, PrizeValue = 10000.00m },
                    ["3"] = new DrawStatsResponsePrizeInfo { Prize = 100, PrizeValue = 500.00m }
                }
            }
        };
        mockLottoService.Setup(x => x.GetDrawPrizes(It.IsAny<string>(), It.IsAny<long>()))
            .ReturnsAsync(apiResponse);

        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType
            {
                Id = 1,
                Name = "Lotto",
                Description = "Lotto",
                TicketPrize = 3.0m,
                UserNumbersCountMin = 2,
                UserNumbersCountMax = 2,
                NumbersCount = 6,
                NumbersMaxValue = 49,
                SpecialsCount = 0,
                SpecialsMaxValue = 0
            };
            db.DrawTypes.Add(drawType);

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 5, 12, 23, 34, 45, 49 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }, mockLottoService).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-prizes-list?drawTypeId=1&drawSystemId=1001");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(3, result.WinTiers.Count);
        Assert.Equal(1001, result.DrawSystemId);
        Assert.Equal(new DateTime(2024, 1, 15), result.DrawDate);
        Assert.Equal("Lotto", result.GameType);

        var tier1 = result.WinTiers.FirstOrDefault(t => t.Tier == "1");
        Assert.NotNull(tier1);
        Assert.Equal(1, tier1.WinsCount);
        Assert.Equal(1000000.00m, tier1.WinsPrize);

        var tier2 = result.WinTiers.FirstOrDefault(t => t.Tier == "2");
        Assert.NotNull(tier2);
        Assert.Equal(5, tier2.WinsCount);
        Assert.Equal(10000.00m, tier2.WinsPrize);
    }

    [Fact]
    public async Task GetPrizesList_ReturnsEmptyPrizes_WhenGameTypeDoesNotMatch()
    {
        // Arrange
        var mockLottoService = new Mock<ILottoOpenApiService>();
        var apiResponse = new List<DrawStatsResponse>
        {
            new DrawStatsResponse
            {
                DrawSystemId = 1001,
                GameType = "DifferentGame",
                Prizes = new Dictionary<string, DrawStatsResponsePrizeInfo>
                {
                    ["1"] = new DrawStatsResponsePrizeInfo { Prize = 1, PrizeValue = 1000000.00m }
                }
            }
        };
        mockLottoService.Setup(x => x.GetDrawPrizes(It.IsAny<string>(), It.IsAny<long>()))
            .ReturnsAsync(apiResponse);

        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType
            {
                Id = 1,
                Name = "Lotto",
                Description = "Lotto",
                TicketPrize = 3.0m,
                UserNumbersCountMin = 2,
                UserNumbersCountMax = 2,
                NumbersCount = 6,
                NumbersMaxValue = 49,
                SpecialsCount = 0,
                SpecialsMaxValue = 0
            };
            db.DrawTypes.Add(drawType);

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 1001,
                DrawDate = new DateTime(2024, 1, 15),
                DrawTypeId = 1,
                Numbers = new List<int> { 5, 12, 23, 34, 45, 49 },
                Specials = new List<int>()
            });
            db.SaveChanges();
        }, mockLottoService).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-prizes-list?drawTypeId=1&drawSystemId=1001");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Empty(result.WinTiers);
    }

    [Fact]
    public async Task GetPrizesList_ReturnsCorrectDrawInfo_ForEuroJackpot()
    {
        // Arrange
        var mockLottoService = new Mock<ILottoOpenApiService>();
        var apiResponse = new List<DrawStatsResponse>
        {
            new DrawStatsResponse
            {
                DrawSystemId = 2001,
                GameType = "EuroJackpot",
                Prizes = new Dictionary<string, DrawStatsResponsePrizeInfo>
                {
                    ["1"] = new DrawStatsResponsePrizeInfo { Prize = 0, PrizeValue = 50000000.00m },
                    ["2"] = new DrawStatsResponsePrizeInfo { Prize = 3, PrizeValue = 500000.00m }
                }
            }
        };
        mockLottoService.Setup(x => x.GetDrawPrizes(It.IsAny<string>(), It.IsAny<long>()))
            .ReturnsAsync(apiResponse);

        var client = CreateFactoryWithData(db =>
        {
            var drawType = new DrawType
            {
                Id = 6,
                Name = "EuroJackpot",
                Description = "EuroJackpot",
                TicketPrize = 12.5m,
                UserNumbersCountMin = 2,
                UserNumbersCountMax = 2,
                NumbersCount = 5,
                NumbersMaxValue = 50,
                SpecialsCount = 2,
                SpecialsMaxValue = 12
            };
            db.DrawTypes.Add(drawType);

            db.Draws.Add(new Draw
            {
                Id = 1,
                DrawSystemId = 2001,
                DrawDate = new DateTime(2024, 2, 10),
                DrawTypeId = 6,
                Numbers = new List<int> { 5, 12, 23, 34, 45 },
                Specials = new List<int> { 3, 7 }
            });
            db.SaveChanges();
        }, mockLottoService).CreateClient();
        client.DefaultRequestHeaders.Add("X-TOKEN", ValidXToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Act
        var response = await client.GetAsync("/api/lotto/draws-get-prizes-list?drawTypeId=6&drawSystemId=2001");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(2, result.WinTiers.Count);
        Assert.Equal(2001, result.DrawSystemId);
        Assert.Equal(new DateTime(2024, 2, 10), result.DrawDate);
        Assert.Equal("EuroJackpot", result.GameType);
    }
}
