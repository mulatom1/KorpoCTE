using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

using App01.Modules.Lotto.Features.FileEdit01;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Lotto.FileEdit01;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string JwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";
    private readonly string _testDirectory;

    public EndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _testDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TMP", "LOTTO", "EDIT");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        // Clean up test files
        if (Directory.Exists(_testDirectory))
        {
            foreach (var file in Directory.GetFiles(_testDirectory, "test_*.txt"))
            {
                try { File.Delete(file); } catch { }
            }
        }
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

    private string CreateTestFile(string fileName, string[] lines)
    {
        var filePath = Path.Combine(_testDirectory, fileName);
        File.WriteAllLines(filePath, lines);
        return filePath;
    }

    [Fact]
    public async Task FileEdit01_ValidationFails_WhenFileNameIsEmpty()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        var request = new Contracts.Request("", 0, 'X');

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/file-edit-01", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FileEdit01_ValidationFails_WhenPositionIsNegative()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        var request = new Contracts.Request("test.txt", -1, 'X');

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/file-edit-01", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FileEdit01_ReturnsFailure_WhenFileDoesNotExist()
    {
        // Arrange
        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        var request = new Contracts.Request("nonexistent_file_12345.txt", 0, 'X');

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/file-edit-01", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Contains("Could not find file", result.Message);
    }

    [Fact]
    public async Task FileEdit01_ReplacesCharacter_InSingleLine()
    {
        // Arrange
        var fileName = $"test_{Guid.NewGuid()}.txt";
        CreateTestFile(fileName, new[] { "Hello World" });

        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        var request = new Contracts.Request(fileName, 0, 'X');

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/file-edit-01", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Equal("OK", result.Message);

        // Verify file content
        var lines = await File.ReadAllLinesAsync(Path.Combine(_testDirectory, fileName));
        Assert.Single(lines);
        Assert.Equal("Xello World", lines[0]);
    }

    [Fact]
    public async Task FileEdit01_ReplacesCharacter_InMultipleLines()
    {
        // Arrange
        var fileName = $"test_{Guid.NewGuid()}.txt";
        CreateTestFile(fileName, new[] { "Line1", "Line2", "Line3" });

        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        var request = new Contracts.Request(fileName, 0, 'X');

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/file-edit-01", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        // Verify file content
        var lines = await File.ReadAllLinesAsync(Path.Combine(_testDirectory, fileName));
        Assert.Equal(3, lines.Length);
        Assert.Equal("Xine1", lines[0]);
        Assert.Equal("Xine2", lines[1]);
        Assert.Equal("Xine3", lines[2]);
    }

    [Fact]
    public async Task FileEdit01_ReplacesCharacter_AtMiddlePosition()
    {
        // Arrange
        var fileName = $"test_{Guid.NewGuid()}.txt";
        CreateTestFile(fileName, new[] { "ABCDEFGH" });

        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        var request = new Contracts.Request(fileName, 3, 'X');

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/file-edit-01", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        // Verify file content
        var lines = await File.ReadAllLinesAsync(Path.Combine(_testDirectory, fileName));
        Assert.Single(lines);
        Assert.Equal("ABCXEFGH", lines[0]);
    }

    [Fact]
    public async Task FileEdit01_SkipsShortLines()
    {
        // Arrange
        var fileName = $"test_{Guid.NewGuid()}.txt";
        CreateTestFile(fileName, new[] { "Short", "VeryLongLine", "AB" });

        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        var request = new Contracts.Request(fileName, 5, 'X');

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/file-edit-01", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        // Verify file content - only lines long enough should be modified
        var lines = await File.ReadAllLinesAsync(Path.Combine(_testDirectory, fileName));
        Assert.Equal(3, lines.Length);
        Assert.Equal("Short", lines[0]); // Not modified (length = 5, position 5 is out of bounds)
        Assert.Equal("VeryLXngLine", lines[1]); // Modified
        Assert.Equal("AB", lines[2]); // Not modified (too short)
    }

    [Fact]
    public async Task FileEdit01_ReturnsFalse_WhenNoChangesMade()
    {
        // Arrange
        var fileName = $"test_{Guid.NewGuid()}.txt";
        CreateTestFile(fileName, new[] { "XBCDEFGH" });

        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        // Request to replace position 0 with 'X', but it's already 'X'
        var request = new Contracts.Request(fileName, 0, 'X');

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/file-edit-01", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.False(result.IsSuccess); // No changes were made
        Assert.Equal("OK", result.Message);
    }

    [Fact]
    public async Task FileEdit01_HandlesEmptyFile()
    {
        // Arrange
        var fileName = $"test_{Guid.NewGuid()}.txt";
        CreateTestFile(fileName, Array.Empty<string>());

        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        var request = new Contracts.Request(fileName, 0, 'X');

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/file-edit-01", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal("OK", result.Message);
    }

    [Fact]
    public async Task FileEdit01_ReplacesDigitWithDigit()
    {
        // Arrange
        var fileName = $"test_{Guid.NewGuid()}.txt";
        CreateTestFile(fileName, new[] { "12345" });

        var client = CreateFactoryWithData().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwtToken());

        var request = new Contracts.Request(fileName, 2, '0');

        // Act
        var response = await client.PostAsJsonAsync("/api/lotto/file-edit-01", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        // Verify file content
        var lines = await File.ReadAllLinesAsync(Path.Combine(_testDirectory, fileName));
        Assert.Single(lines);
        Assert.Equal("12045", lines[0]);
    }
}