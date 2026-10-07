using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using App01.Modules.Courses.Features.CourseTiles;
using App01.Shared.Application.Entities.Courses;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;


namespace App01.Bootstrapper.Api.Tests.Features.Courses.CourseTiles;

// Przypadki 400 i 401 nie dotyczą tego endpointu:
// - 400: żądanie nie ma żadnych pól, więc nie ma czego walidować,
// - 401: endpoint jest publiczny (bez RequireAuthorization), brak lub błędny JWT daje 200.
public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _contentPath;
    private const string XToken = "test-x-token";
    private const string Url = "/api/courses/course-tiles";
    private const string MediaUrlBase = "/media/courses";

    // Stała chwila "teraz" dla testów
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    public EndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _contentPath = Path.Combine(Path.GetTempPath(), $"CourseTilesTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentPath))
        {
            Directory.Delete(_contentPath, recursive: true);
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
    }

    private WebApplicationFactory<Program> CreateFactory(Action<AppDbContext>? seedData = null, string? contentPath = null, string? webRoot = null)
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
                    ["Jwt:Key"] = "ThisIsASecretKeyForTestingPurposesOnly123456",
                    ["Jwt:Issuer"] = "TestIssuer",
                    ["Jwt:Audience"] = "TestAudience",
                    ["Jwt:ExpiryInMinutes"] = "60",
                    ["Swagger:Enabled"] = "false",
                    ["Tokens:X-TOKEN"] = XToken,
                    ["ConnectionStrings:DefaultConnection"] = "Server=dummy;Database=dummy;Integrated Security=True;",
                    ["Courses:ContentPath"] = contentPath ?? _contentPath,
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

    private HttpClient CreateClient(WebApplicationFactory<Program> factory, bool withXToken = true)
    {
        var client = factory.CreateClient();
        if (withXToken)
        {
            client.DefaultRequestHeaders.Add("X-TOKEN", XToken);
        }
        return client;
    }

    private void WriteCourseFile(string slug, string content)
    {
        var dir = Path.Combine(_contentPath, slug);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, slug + ".md"), content);
    }

    private void WriteValidCourse(string slug, string title, string? image = null)
    {
        var imageLine = image is null ? string.Empty : $"image: {image}\n";
        WriteCourseFile(slug, $"---\ntitle: {title}\ndescription: Opis {slug}\n{imageLine}---\n\n# Treść\n");
    }

    private static async Task<Contracts.Response> ReadResponse(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Fallback SPA też zwraca 200 - sprawdzamy, że to JSON z kontraktu
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var result = await response.Content.ReadFromJsonAsync<Contracts.Response>();
        Assert.NotNull(result);
        Assert.NotNull(result.Courses);
        return result;
    }

    [Fact]
    public async Task CourseTiles_WithPublishedCourse_ReturnsFullTile()
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
            "unknownKey: ignorowany\r\n" +
            "---\r\n" +
            "\r\n" +
            "# Treść kursu\r\n");
        var client = CreateClient(CreateFactory(db =>
            db.Courses.Add(new Course { Id = 1, Slug = "korpo-cte-300", PublishDate = Now.AddDays(-1) })));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        var result = await ReadResponse(response);
        var tile = Assert.Single(result.Courses);
        Assert.Equal("korpo-cte-300", tile.Slug);
        Assert.Equal("Korpo CTE 300", tile.Title);
        Assert.Equal("Krótki opis kursu", tile.ShortDescription);
        Assert.Equal(["sql", "cte"], tile.Tags);
        Assert.Equal("/media/courses/korpo-cte-300/cover.png", tile.ImageUrl);
    }

    [Fact]
    public async Task CourseTiles_DoesNotExposePublishDateOrContent()
    {
        // Arrange
        WriteValidCourse("kurs-a", "Kurs A");
        var client = CreateClient(CreateFactory(db =>
            db.Courses.Add(new Course { Id = 1, Slug = "kurs-a", PublishDate = Now.AddDays(-1) })));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"courses\"", content);
        Assert.DoesNotContain("publishDate", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Treść", content);
    }

    [Fact]
    public async Task CourseTiles_PublishDateEqualToNow_IsVisible_FutureIsHidden()
    {
        // Arrange
        WriteValidCourse("teraz", "Teraz");
        WriteValidCourse("za-minute", "Za minutę");
        var client = CreateClient(CreateFactory(db =>
        {
            db.Courses.Add(new Course { Id = 1, Slug = "teraz", PublishDate = Now });
            db.Courses.Add(new Course { Id = 2, Slug = "za-minute", PublishDate = Now.AddMinutes(1) });
        }));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        var result = await ReadResponse(response);
        var tile = Assert.Single(result.Courses);
        Assert.Equal("teraz", tile.Slug);
    }

    [Fact]
    public async Task CourseTiles_SortsByPublishDateThenTitleThenId()
    {
        // Arrange
        WriteValidCourse("wczesny", "Zeta");
        WriteValidCourse("beta", "Beta");
        WriteValidCourse("alfa", "alfa");
        WriteValidCourse("lodz", "Łódź");
        WriteValidCourse("lublin", "Lublin");
        WriteValidCourse("duplikat-2", "Duplikat");
        WriteValidCourse("duplikat-1", "Duplikat");
        var client = CreateClient(CreateFactory(db =>
        {
            db.Courses.Add(new Course { Id = 1, Slug = "beta", PublishDate = Now.AddDays(-1) });
            db.Courses.Add(new Course { Id = 2, Slug = "alfa", PublishDate = Now.AddDays(-1) });
            db.Courses.Add(new Course { Id = 3, Slug = "wczesny", PublishDate = Now.AddDays(-10) });
            db.Courses.Add(new Course { Id = 4, Slug = "lodz", PublishDate = Now.AddDays(-1) });
            db.Courses.Add(new Course { Id = 5, Slug = "lublin", PublishDate = Now.AddDays(-1) });
            db.Courses.Add(new Course { Id = 7, Slug = "duplikat-2", PublishDate = Now.AddDays(-1) });
            db.Courses.Add(new Course { Id = 6, Slug = "duplikat-1", PublishDate = Now.AddDays(-1) });
        }));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        var result = await ReadResponse(response);
        Assert.Equal(
            ["wczesny", "alfa", "beta", "duplikat-1", "duplikat-2", "lublin", "lodz"],
            result.Courses.Select(c => c.Slug).ToList());
    }

    [Fact]
    public async Task CourseTiles_SkipsBrokenCourses_AndKeepsValidOnes()
    {
        // Arrange
        WriteValidCourse("poprawny", "Poprawny");
        // brak pliku dla "brak-pliku"
        WriteCourseFile("bez-frontmattera", "# Sam Markdown bez frontmattera\n");
        WriteCourseFile("zly-yaml", "---\ntitle: [niezamknięta\ndescription: Opis\n---\n");
        WriteCourseFile("bez-tytulu", "---\ndescription: Opis bez tytułu\n---\n");
        WriteCourseFile("niezamkniety", "---\ntitle: Tytuł\ndescription: Opis\n");
        var client = CreateClient(CreateFactory(db =>
        {
            db.Courses.Add(new Course { Id = 1, Slug = "poprawny", PublishDate = Now.AddDays(-1) });
            db.Courses.Add(new Course { Id = 2, Slug = "brak-pliku", PublishDate = Now.AddDays(-1) });
            db.Courses.Add(new Course { Id = 3, Slug = "bez-frontmattera", PublishDate = Now.AddDays(-1) });
            db.Courses.Add(new Course { Id = 4, Slug = "zly-yaml", PublishDate = Now.AddDays(-1) });
            db.Courses.Add(new Course { Id = 5, Slug = "bez-tytulu", PublishDate = Now.AddDays(-1) });
            db.Courses.Add(new Course { Id = 6, Slug = "niezamkniety", PublishDate = Now.AddDays(-1) });
        }));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        var result = await ReadResponse(response);
        var tile = Assert.Single(result.Courses);
        Assert.Equal("poprawny", tile.Slug);
    }

    [Theory]
    [InlineData("../etc")]
    [InlineData("kurs/podkatalog")]
    [InlineData("kurs\\podkatalog")]
    [InlineData("Wielkie-Litery")]
    [InlineData("-od-myslnika")]
    [InlineData("..")]
    public async Task CourseTiles_WithInvalidSlug_SkipsCourse(string invalidSlug)
    {
        // Arrange
        WriteValidCourse("poprawny", "Poprawny");
        // Slug bez znaków ścieżki dostaje poprawny plik - kurs ma odpaść przez regułę sluga, nie przez brak pliku
        if (!invalidSlug.Contains('/') && !invalidSlug.Contains('\\') && !invalidSlug.Contains(".."))
        {
            WriteValidCourse(invalidSlug, "Niedozwolony slug");
        }
        var client = CreateClient(CreateFactory(db =>
        {
            db.Courses.Add(new Course { Id = 1, Slug = "poprawny", PublishDate = Now.AddDays(-1) });
            db.Courses.Add(new Course { Id = 2, Slug = invalidSlug, PublishDate = Now.AddDays(-1) });
        }));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        var result = await ReadResponse(response);
        var tile = Assert.Single(result.Courses);
        Assert.Equal("poprawny", tile.Slug);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("../x.png")]
    [InlineData("podkatalog/x.png")]
    [InlineData("podkatalog\\\\x.png")]
    public async Task CourseTiles_WithMissingOrInvalidImage_ReturnsNullImageUrl(string? image)
    {
        // Arrange
        var imageLine = image is null ? string.Empty : $"image: \"{image}\"\n";
        WriteCourseFile("kurs-a", $"---\ntitle: Kurs A\ndescription: Opis\n{imageLine}---\n");
        var client = CreateClient(CreateFactory(db =>
            db.Courses.Add(new Course { Id = 1, Slug = "kurs-a", PublishDate = Now.AddDays(-1) })));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        var result = await ReadResponse(response);
        var tile = Assert.Single(result.Courses);
        Assert.Equal("Kurs A", tile.Title);
        Assert.Null(tile.ImageUrl);
        Assert.Empty(tile.Tags);
    }

    [Fact]
    public async Task CourseTiles_WithEmptyTable_ReturnsEmptyList()
    {
        // Arrange
        var client = CreateClient(CreateFactory());

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        var result = await ReadResponse(response);
        Assert.Empty(result.Courses);
    }

    [Fact]
    public async Task CourseTiles_WithoutContentPathConfigured_ReturnsEmptyList()
    {
        // Arrange
        WriteValidCourse("kurs-a", "Kurs A");
        var client = CreateClient(CreateFactory(
            db => db.Courses.Add(new Course { Id = 1, Slug = "kurs-a", PublishDate = Now.AddDays(-1) }),
            contentPath: string.Empty));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        var result = await ReadResponse(response);
        Assert.Empty(result.Courses);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CourseTiles_WithContentPathInsideWebRoot_ReturnsEmptyList(bool webRootEqualsContentPath)
    {
        // Arrange - poprawny, opublikowany kurs w ContentPath; bez bezpiecznika lista miałaby jeden kafelek.
        // Webroot = _contentPath, a ContentPath to ten sam katalog albo jego podkatalog z kopią kursu.
        WriteValidCourse("kurs-a", "Kurs A");
        var contentPath = _contentPath;
        if (!webRootEqualsContentPath)
        {
            contentPath = Path.Combine(_contentPath, "media", "courses");
            var dir = Path.Combine(contentPath, "kurs-a");
            Directory.CreateDirectory(dir);
            File.Copy(Path.Combine(_contentPath, "kurs-a", "kurs-a.md"), Path.Combine(dir, "kurs-a.md"));
        }
        var client = CreateClient(CreateFactory(
            db => db.Courses.Add(new Course { Id = 1, Slug = "kurs-a", PublishDate = Now.AddDays(-1) }),
            contentPath: contentPath,
            webRoot: _contentPath));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        var result = await ReadResponse(response);
        Assert.Empty(result.Courses);
    }

    [Fact]
    public async Task CourseTiles_WithoutJwt_ReturnsOk()
    {
        // Arrange
        WriteValidCourse("kurs-a", "Kurs A");
        var client = CreateClient(CreateFactory(db =>
            db.Courses.Add(new Course { Id = 1, Slug = "kurs-a", PublishDate = Now.AddDays(-1) })));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        var result = await ReadResponse(response);
        Assert.Single(result.Courses);
    }

    [Fact]
    public async Task CourseTiles_WithInvalidJwt_ReturnsOk()
    {
        // Arrange
        WriteValidCourse("kurs-a", "Kurs A");
        var client = CreateClient(CreateFactory(db =>
            db.Courses.Add(new Course { Id = 1, Slug = "kurs-a", PublishDate = Now.AddDays(-1) })));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        var result = await ReadResponse(response);
        Assert.Single(result.Courses);
    }

    [Fact]
    public async Task CourseTiles_WithoutXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateClient(CreateFactory(), withXToken: false);

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CourseTiles_WithInvalidXToken_ReturnsForbidden()
    {
        // Arrange
        var client = CreateClient(CreateFactory(), withXToken: false);
        client.DefaultRequestHeaders.Add("X-TOKEN", "invalid-token");

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}