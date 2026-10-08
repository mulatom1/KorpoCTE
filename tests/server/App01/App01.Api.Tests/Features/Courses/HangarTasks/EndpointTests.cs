using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

using App01.Modules.Courses.Features.HangarTasks;
using App01.Shared.Application.Entities.Courses;
using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Courses.HangarTasks;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string JwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";
    private const string XToken = "test-x-token";
    private const string Url = "/api/courses/hangar-tasks";
    private const long CurrentUserId = 2;
    private const long OtherUserId = 3;
    private const string SecretCriteria = "TAJNE-KRYTERIUM-XYZ";

    // Stała chwila "teraz" dla testów
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    public EndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTime utcNow)
        {
            _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private WebApplicationFactory<Program> CreateFactory(Action<AppDbContext>? seedData = null)
    {
        // Stała nazwa bazy na fabrykę - seed i żądania widzą te same dane
        var dbName = $"TestDb_{Guid.NewGuid()}";

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
                    ["Tokens:X-TOKEN"] = XToken,
                    ["ConnectionStrings:DefaultConnection"] = "Server=dummy;Database=dummy;Integrated Security=True;"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Usunięcie workerów w tle, żeby nie uruchamiały się podczas testów
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

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName);
                });

                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));

                if (seedData != null)
                {
                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.Database.EnsureCreated();
                    seedData(db);
                    db.SaveChanges();
                }
            });
        });
    }

    private static string GenerateJwtToken(long userId, string email, bool isAdmin)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("isAdmin", isAdmin.ToString().ToLower()),
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

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, bool withXToken = true, bool withJwt = true)
    {
        var client = factory.CreateClient();
        if (withXToken)
        {
            client.DefaultRequestHeaders.Add("X-TOKEN", XToken);
        }
        if (withJwt)
        {
            var token = GenerateJwtToken(CurrentUserId, "user1@example.com", isAdmin: false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    // Kurs opublikowany wcześniej (1), później (2, równo "teraz") i nieopublikowany (3).
    // Flaga 10 zdobyta przez bieżącego użytkownika, flaga 11 tylko przez innego użytkownika.
    private static void SeedTasks(AppDbContext db)
    {
        db.Users.AddRange(
            new User { Id = CurrentUserId, Email = "user1@example.com", CreatedAt = Now },
            new User { Id = OtherUserId, Email = "user2@example.com", CreatedAt = Now });

        db.Courses.AddRange(
            new Course { Id = 1, Slug = "kurs-starszy", PublishDate = Now.AddDays(-10) },
            new Course { Id = 2, Slug = "kurs-nowszy", PublishDate = Now },
            new Course { Id = 3, Slug = "kurs-przyszly", PublishDate = Now.AddMinutes(1) });

        db.Flags.AddRange(
            new Flag { Id = 12, CourseId = 2, Code = "N-1", Title = "Zadanie nowsze", Criteria = SecretCriteria },
            new Flag { Id = 11, CourseId = 1, Code = "S-2", Title = "Zadanie starsze 2", Criteria = SecretCriteria },
            new Flag { Id = 10, CourseId = 1, Code = "S-1", Title = "Zadanie starsze 1", Criteria = SecretCriteria },
            new Flag { Id = 13, CourseId = 1, Code = "S-NULL", Title = "Bez kryteriów", Criteria = null },
            new Flag { Id = 14, CourseId = 1, Code = "S-EMPTY", Title = "Puste kryteria", Criteria = "" },
            new Flag { Id = 16, CourseId = 1, Code = "S-WS", Title = "Białe znaki", Criteria = "  \t\n " },
            new Flag { Id = 15, CourseId = 3, Code = "P-1", Title = "Zadanie przyszłe", Criteria = SecretCriteria });

        db.UserFlags.AddRange(
            new UserFlag { Id = 1, UserId = CurrentUserId, FlagId = 10, EarnedAt = Now.AddDays(-1) },
            new UserFlag { Id = 2, UserId = OtherUserId, FlagId = 11, EarnedAt = Now.AddDays(-1) });
    }

    [Fact]
    public async Task HangarTasks_WithPublishedTasks_ReturnsTasksWithOwnershipOfCurrentUser()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedTasks));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Fallback SPA też zwraca 200 - sprawdzamy, że to JSON z kontraktu
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Equal(
            [
                new Contracts.HangarTaskDto(10, "kurs-starszy", "Zadanie starsze 1", true),
                new Contracts.HangarTaskDto(11, "kurs-starszy", "Zadanie starsze 2", false),
                new Contracts.HangarTaskDto(12, "kurs-nowszy", "Zadanie nowsze", false)
            ],
            result.Tasks);
    }

    [Fact]
    public async Task HangarTasks_SkipsUnpublishedCourseAndFlagsWithoutCriteria()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedTasks));

        // Act
        var result = await client.GetFromJsonAsync<Contracts.Response>(Url);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain(result.Tasks, t => t.FlagId is 13 or 14 or 15 or 16);
        Assert.DoesNotContain(result.Tasks, t => t.CourseSlug == "kurs-przyszly");
    }

    [Fact]
    public async Task HangarTasks_ResponseDoesNotContainCriteria()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedTasks));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("criteria", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SecretCriteria, json);
        // Code to sekret aktywacji flagi - lista zadań nigdy go nie zwraca
        Assert.DoesNotContain("\"code\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("S-1", json);
        Assert.DoesNotContain("N-1", json);
    }

    [Fact]
    public async Task HangarTasks_WithoutFlags_ReturnsEmptyList()
    {
        // Arrange
        var client = CreateClient(CreateFactory());

        // Act
        var result = await client.GetFromJsonAsync<Contracts.Response>(Url);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Tasks);
    }

    [Fact]
    public async Task HangarTasks_WithoutJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedTasks), withJwt: false);

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HangarTasks_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateClient(CreateFactory(SeedTasks), withXToken: false);

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}