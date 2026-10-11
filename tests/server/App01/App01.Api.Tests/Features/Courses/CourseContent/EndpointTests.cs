using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using App01.Modules.Courses.Features.CourseContent;
using App01.Shared.Application.Entities.Courses;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;


namespace App01.Bootstrapper.Api.Tests.Features.Courses.CourseContent;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _tempRoot;
    private readonly string _contentPath;
    private const string JwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";
    private const string XToken = "test-x-token";
    private const string Url = "/api/courses/course-content";
    private const string MediaUrlBase = "/media/courses/";
    private const string NotFoundMessage = "Kurs nie istnieje lub nie jest jeszcze opublikowany";

    // Unikalny znacznik treści kursu - jego obecność w odpowiedzi oznacza wyciek treści
    private const string ContentSentinel = "WARTOWNIK-TRESCI-7f3c9a1e";

    // Stała chwila "teraz" dla testów
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    public EndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        // Treść w podkatalogu katalogu tymczasowego - test bezpiecznika ustawia webroot na katalog nadrzędny
        _tempRoot = Path.Combine(Path.GetTempPath(), $"CourseContentTests_{Guid.NewGuid():N}");
        _contentPath = Path.Combine(_tempRoot, "content");
        Directory.CreateDirectory(_contentPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTime utcNow)
        {
            _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        // Stała strefa lokalna różna od UTC: pomyłka "czas lokalny zamiast UTC" przesuwa granicę
        // publikacji na każdej maszynie (także na agencie CI w UTC)
        public override TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.CreateCustomTimeZone("Test+05:00", TimeSpan.FromHours(5), "Test+05:00", "Test+05:00");
    }

    private WebApplicationFactory<Program> CreateFactory(Action<AppDbContext>? seedData = null, string? webRoot = null)
    {
        // Stała nazwa bazy na fabrykę - seed i żądania widzą te same dane
        var dbName = $"TestDb_{Guid.NewGuid()}";

        return _factory.WithWebHostBuilder(builder =>
        {
            if (webRoot is not null)
            {
                builder.UseSetting(WebHostDefaults.WebRootKey, webRoot);
            }

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
                    ["ConnectionStrings:DefaultConnection"] = "Server=dummy;Database=dummy;Integrated Security=True;",
                    ["Courses:ContentPath"] = _contentPath,
                    // Końcowy '/' ma zostać obcięty przy budowaniu MediaBaseUrl
                    ["Courses:MediaUrlBase"] = MediaUrlBase
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

    private static string GenerateJwtToken(int userId, string email, bool isAdmin, bool expired = false)
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
            // Walidacja JWT używa zegara rzeczywistego (ClockSkew = 0), nie TimeProvider
            notBefore: expired ? DateTime.UtcNow.AddHours(-2) : null,
            expires: expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(1),
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
            var token = GenerateJwtToken(2, "user1@example.com", isAdmin: false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    private static string UrlFor(string slug) => $"{Url}?slug={Uri.EscapeDataString(slug)}";

    private void WriteCourseFile(string slug, string content)
    {
        var dir = Path.Combine(_contentPath, slug);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, slug + ".md"), content);
    }

    private void WriteValidCourse(string slug)
    {
        WriteCourseFile(slug, $"---\ntitle: Kurs {slug}\ndescription: Opis {slug}\n---\n\n# Treść {slug}\n");
    }

    private const string SpaIndexMarker = "SPA-INDEX-MARKER";

    // Układ jak na hoście: <tmp>/wwwroot/media/courses/<slug>/ z mediami i katalog-rodzeństwo <tmp>/content
    // z <slug>/<slug>.md (ContentPath = ../courses-content poza katalogiem witryny). Zwraca ścieżkę webroota.
    private string CreateProductionLayout(string slug, bool withSpaIndex)
    {
        var webRoot = Path.Combine(_tempRoot, "wwwroot");
        var mediaDir = Path.Combine(webRoot, "media", "courses", slug);
        Directory.CreateDirectory(mediaDir);
        File.WriteAllBytes(Path.Combine(mediaDir, "thumbnail.png"), [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        if (withSpaIndex)
        {
            File.WriteAllText(Path.Combine(webRoot, "index.html"), $"<!doctype html><div id=\"root\">{SpaIndexMarker}</div>");
        }

        WriteCourseWithSentinel(slug);
        return webRoot;
    }

    // Kurs z wartownikiem w tytule i w treści Markdown
    private void WriteCourseWithSentinel(string slug)
    {
        WriteCourseFile(slug,
            $"---\ntitle: Kurs {slug} {ContentSentinel}\ndescription: Opis {slug}\n---\n\n# Treść {slug}\n\n{ContentSentinel}\n");
    }

    private static async Task AssertNotFound(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(NotFoundMessage, body);
    }

    [Fact]
    public async Task CourseContent_WithPublishedCourse_ReturnsContentWithoutFrontmatter()
    {
        // Arrange
        WriteCourseFile("korpo-cte-300",
            "﻿---\r\n" +
            "title: Korpo CTE 300\r\n" +
            "description: Krótki opis kursu\r\n" +
            "tags:\r\n" +
            "  - sql\r\n" +
            "  - cte\r\n" +
            "image: cover.png\r\n" +
            "---\r\n" +
            "\r\n" +
            "\r\n" +
            "# Treść kursu\r\n" +
            "\r\n" +
            "![Obrazek](images/01.png)\r\n");
        var client = CreateClient(CreateFactory(db =>
            db.Courses.Add(new Course { Id = 1, Slug = "korpo-cte-300", PublishDate = Now.AddDays(-1) })));

        // Act
        var response = await client.GetAsync(UrlFor("korpo-cte-300"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Fallback SPA też zwraca 200 - sprawdzamy, że to JSON z kontraktu
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Equal("korpo-cte-300", root.GetProperty("slug").GetString());
        Assert.Equal("Korpo CTE 300", root.GetProperty("title").GetString());
        Assert.Equal(["sql", "cte"], root.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToList());
        Assert.Equal("# Treść kursu\n\n![Obrazek](images/01.png)\n", root.GetProperty("content").GetString());
        Assert.Equal("/media/courses/korpo-cte-300", root.GetProperty("mediaBaseUrl").GetString());
        Assert.DoesNotContain("publishDate", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("description", json, StringComparison.OrdinalIgnoreCase);
    }

    // Wyrocznia (decyzja S-04): kurs widoczny od chwili PublishDate w UTC, równość = widoczny
    [Theory]
    [InlineData(-1L, true)]
    [InlineData(0L, true)]
    [InlineData(1L, false)]
    public async Task CourseContent_PublishDateBoundary_VisibleOnlyFromPublishInstantUtc(long offsetTicks, bool expectVisible)
    {
        // Arrange - data z bazy bez Kind (tak EF czyta datetime2)
        WriteCourseWithSentinel("granica");
        var publishDate = DateTime.SpecifyKind(Now.AddTicks(offsetTicks), DateTimeKind.Unspecified);
        var client = CreateClient(CreateFactory(db =>
            db.Courses.Add(new Course { Id = 1, Slug = "granica", PublishDate = publishDate })));

        // Act
        var response = await client.GetAsync(UrlFor("granica"));

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        if (expectVisible)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(ContentSentinel, body);
        }
        else
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Contains(NotFoundMessage, body);
            Assert.DoesNotContain(ContentSentinel, body);
        }
    }

    [Fact]
    public async Task CourseContent_SlugNotInDatabase_ReturnsNotFound()
    {
        // Arrange - plik istnieje, ale kursu nie ma w bazie
        WriteValidCourse("tylko-plik");
        var client = CreateClient(CreateFactory(db =>
            db.Courses.Add(new Course { Id = 1, Slug = "inny-kurs", PublishDate = Now.AddDays(-1) })));

        // Act
        var response = await client.GetAsync(UrlFor("tylko-plik"));

        // Assert
        await AssertNotFound(response);
    }

    [Fact]
    public async Task CourseContent_CourseWithoutFile_ReturnsNotFound()
    {
        // Arrange
        var client = CreateClient(CreateFactory(db =>
            db.Courses.Add(new Course { Id = 1, Slug = "brak-pliku", PublishDate = Now.AddDays(-1) })));

        // Act
        var response = await client.GetAsync(UrlFor("brak-pliku"));

        // Assert
        await AssertNotFound(response);
    }

    [Theory]
    [InlineData("# Sam Markdown bez frontmattera\n")]
    [InlineData("---\ntitle: [niezamknięta\ndescription: Opis\n---\n# Treść\n")]
    [InlineData("---\ndescription: Opis bez tytułu\n---\n# Treść\n")]
    [InlineData("---\ntitle: Tytuł\ndescription: Opis\n# Treść\n")]
    public async Task CourseContent_WithInvalidFrontmatter_ReturnsNotFound(string fileContent)
    {
        // Arrange
        WriteCourseFile("zly-kurs", fileContent);
        var client = CreateClient(CreateFactory(db =>
            db.Courses.Add(new Course { Id = 1, Slug = "zly-kurs", PublishDate = Now.AddDays(-1) })));

        // Act
        var response = await client.GetAsync(UrlFor("zly-kurs"));

        // Assert
        await AssertNotFound(response);
    }

    [Theory]
    [InlineData("Wielkie")]
    [InlineData("../etc")]
    [InlineData("-od-myslnika")]
    [InlineData("kurs/podkatalog")]
    public async Task CourseContent_WithInvalidSlug_ReturnsBadRequest(string slug)
    {
        // Arrange
        var client = CreateClient(CreateFactory());

        // Act
        var response = await client.GetAsync(UrlFor(slug));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CourseContent_WithTooLongSlug_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateClient(CreateFactory());

        // Act
        var response = await client.GetAsync(UrlFor(new string('a', 101)));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CourseContent_WithoutSlugParameter_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateClient(CreateFactory());

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Identyfikator kursu jest wymagany", body);
    }

    [Theory]
    [InlineData("brak", true)]
    [InlineData("brak", false)]
    [InlineData("nieprawidlowy", true)]
    [InlineData("nieprawidlowy", false)]
    [InlineData("wygasly", true)]
    [InlineData("wygasly", false)]
    public async Task CourseContent_WithoutValidJwt_ReturnsUnauthorizedWithoutContent(string tokenVariant, bool published)
    {
        // Arrange - kurs opublikowany albo z datą publikacji w przyszłości, plik z wartownikiem istnieje
        WriteCourseWithSentinel("kurs-a");
        var publishDate = published ? Now.AddDays(-1) : Now.AddDays(1);
        var client = CreateClient(CreateFactory(db =>
            db.Courses.Add(new Course { Id = 1, Slug = "kurs-a", PublishDate = publishDate })), withJwt: false);
        var token = tokenVariant switch
        {
            "brak" => null,
            "nieprawidlowy" => "invalid-token",
            "wygasly" => GenerateJwtToken(2, "user1@example.com", isAdmin: false, expired: true),
            _ => throw new ArgumentOutOfRangeException(nameof(tokenVariant))
        };
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Act
        var response = await client.GetAsync(UrlFor("kurs-a"));

        // Assert - sam status nie wystarcza: ciało nie może zawierać treści ani tytułu kursu
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ContentSentinel, body);
    }

    [Fact]
    public async Task CourseContent_WithValidJwt_ReturnsSentinelContent()
    {
        // Arrange - kontrola pozytywna dla testu 401: ten sam seed z ważnym JWT zwraca wartownika
        WriteCourseWithSentinel("kurs-a");
        var client = CreateClient(CreateFactory(db =>
            db.Courses.Add(new Course { Id = 1, Slug = "kurs-a", PublishDate = Now.AddDays(-1) })));

        // Act
        var response = await client.GetAsync(UrlFor("kurs-a"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.Contains(ContentSentinel, result.Content);
        Assert.Contains(ContentSentinel, result.Title);
    }

    [Fact]
    public async Task CourseContent_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        WriteValidCourse("kurs-a");
        var client = CreateClient(CreateFactory(db =>
            db.Courses.Add(new Course { Id = 1, Slug = "kurs-a", PublishDate = Now.AddDays(-1) })), withXToken: false);

        // Act
        var response = await client.GetAsync(UrlFor("kurs-a"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CourseContent_WithContentPathInsideWebRoot_ReturnsNotFound(bool webRootEqualsContentPath)
    {
        // Arrange - poprawny, opublikowany kurs; bez bezpiecznika endpoint zwróciłby 200
        WriteValidCourse("kurs-a");
        var webRoot = webRootEqualsContentPath ? _contentPath : _tempRoot;
        var client = CreateClient(CreateFactory(
            db => db.Courses.Add(new Course { Id = 1, Slug = "kurs-a", PublishDate = Now.AddDays(-1) }),
            webRoot: webRoot));

        // Act
        var response = await client.GetAsync(UrlFor("kurs-a"));

        // Assert
        await AssertNotFound(response);
    }

    [Fact]
    public async Task CourseContent_WithWebRootNextToContentPath_ReturnsOk()
    {
        // Arrange - katalog o wspólnym prefiksie nazwy ("content" vs "content-www") nie uruchamia bezpiecznika
        WriteValidCourse("kurs-a");
        var webRoot = _contentPath + "-www";
        Directory.CreateDirectory(webRoot);
        var client = CreateClient(CreateFactory(
            db => db.Courses.Add(new Course { Id = 1, Slug = "kurs-a", PublishDate = Now.AddDays(-1) }),
            webRoot: webRoot));

        // Act
        var response = await client.GetAsync(UrlFor("kurs-a"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CourseContentFile_IsNotReachableUnderPublicUrls(bool withSpaIndex)
    {
        // Arrange - produkcyjny układ katalogów; webroot z index.html SPA albo bez niego
        const string slug = "kurs-a";
        var webRoot = CreateProductionLayout(slug, withSpaIndex);
        var factory = CreateFactory(
            db => db.Courses.Add(new Course { Id = 1, Slug = slug, PublishDate = Now.AddDays(-1) }),
            webRoot: webRoot);
        var anonymousClient = CreateClient(factory, withXToken: false, withJwt: false);

        // Kandydackie adresy pliku treści; '..' zakodowane, bo HttpClient normalizuje segmenty kropek
        string[] candidateUrls =
        [
            $"/media/courses/{slug}/{slug}.md",
            $"/{slug}/{slug}.md",
            $"/content/{slug}/{slug}.md",
            $"/courses-content/{slug}/{slug}.md",
            $"/media/courses/{slug}/",
            $"/media/courses/%2e%2e/%2e%2e/content/{slug}/{slug}.md"
        ];

        // Act & Assert - o wycieku decyduje ciało i typ treści, nie status (fallback SPA może dać 200)
        foreach (var url in candidateUrls)
        {
            var response = await anonymousClient.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            Assert.False(body.Contains(ContentSentinel), $"Treść kursu wyciekła pod adresem {url} (status {(int)response.StatusCode})");
            Assert.NotEqual("text/markdown", response.Content.Headers.ContentType?.MediaType);
        }

        // Kontrola pozytywna: webroot jest naprawdę serwowany
        var thumbnail = await anonymousClient.GetAsync($"/media/courses/{slug}/thumbnail.png");
        Assert.Equal(HttpStatusCode.OK, thumbnail.StatusCode);
        Assert.Equal("image/png", thumbnail.Content.Headers.ContentType?.MediaType);

        // Kontrola pozytywna: fallback SPA jest aktywny, gdy index.html istnieje
        if (withSpaIndex)
        {
            var spa = await anonymousClient.GetAsync("/nie-istnieje");
            Assert.Equal(HttpStatusCode.OK, spa.StatusCode);
            Assert.Contains(SpaIndexMarker, await spa.Content.ReadAsStringAsync());
        }

        // Kontrola pozytywna: ten sam ContentPath jest używany przez API (z JWT i X-TOKEN)
        var apiResponse = await CreateClient(factory).GetAsync(UrlFor(slug));
        Assert.Equal(HttpStatusCode.OK, apiResponse.StatusCode);
        Assert.Contains(ContentSentinel, await apiResponse.Content.ReadAsStringAsync());
    }
}