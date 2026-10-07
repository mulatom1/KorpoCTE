using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;


namespace App01.Modules.Courses.Content;

// Metadane kursu z frontmattera YAML pliku <ContentPath>/<Slug>/<Slug>.md
public record CourseFrontmatter(
    string Title,
    string Description,
    IReadOnlyList<string> Tags,
    string? Image
);

public interface ICourseFrontmatterReader
{
    // Zwraca metadane albo null (z ostrzeżeniem w logu); nigdy nie rzuca wyjątku do wywołującego
    Task<CourseFrontmatter?> ReadAsync(string slug, CancellationToken cancellationToken);
}

public partial class CourseFrontmatterReader : ICourseFrontmatterReader
{
    private const string FrontmatterDelimiter = "---";

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private readonly IConfiguration _configuration;
    private readonly ILogger<CourseFrontmatterReader> _logger;


    public CourseFrontmatterReader(
        IConfiguration configuration,
        ILogger<CourseFrontmatterReader> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    // Dozwolony slug: małe litery, cyfry, '-' i '_', bez separatorów ścieżki i bez '..'
    [GeneratedRegex("^[a-z0-9][a-z0-9_-]*$")]
    private static partial Regex SlugRegex();

    public static bool IsValidSlug(string? slug) =>
        !string.IsNullOrEmpty(slug) && SlugRegex().IsMatch(slug);

    public async Task<CourseFrontmatter?> ReadAsync(string slug, CancellationToken cancellationToken)
    {
        try
        {
            if (!IsValidSlug(slug))
            {
                _logger.LogWarning("Course skipped: invalid slug {Slug}", slug);
                return null;
            }

            var contentRoot = GetContentRoot();
            if (contentRoot is null)
            {
                return null;
            }

            // Ścieżka budowana wyłącznie ze sluga z bazy; po normalizacji musi leżeć wewnątrz ContentPath
            var filePath = Path.GetFullPath(Path.Combine(contentRoot, slug, slug + ".md"));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!filePath.StartsWith(contentRoot, comparison))
            {
                _logger.LogWarning("Course skipped: path for slug {Slug} is outside of content directory", slug);
                return null;
            }

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Course skipped: content file not found for slug {Slug} ({FilePath})", slug, filePath);
                return null;
            }

            var text = await File.ReadAllTextAsync(filePath, cancellationToken);

            var yaml = ExtractFrontmatter(text);
            if (yaml is null)
            {
                _logger.LogWarning("Course skipped: missing frontmatter block in file for slug {Slug}", slug);
                return null;
            }

            var data = YamlDeserializer.Deserialize<FrontmatterYaml?>(yaml);
            if (data is null || string.IsNullOrWhiteSpace(data.Title) || string.IsNullOrWhiteSpace(data.Description))
            {
                _logger.LogWarning("Course skipped: frontmatter for slug {Slug} lacks required title or description", slug);
                return null;
            }

            var tags = (data.Tags ?? [])
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .ToList();

            var image = string.IsNullOrWhiteSpace(data.Image) ? null : data.Image.Trim();

            return new CourseFrontmatter(data.Title.Trim(), data.Description.Trim(), tags, image);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Błąd YAML, IO lub inny - kurs pomijany, lista działa dalej (FR-010)
            _logger.LogWarning(ex, "Course skipped: failed to read frontmatter for slug {Slug}", slug);
            return null;
        }
    }

    // Katalog treści kursów; ścieżka względna liczona względem AppContext.BaseDirectory
    private string? GetContentRoot()
    {
        var configured = _configuration["Courses:ContentPath"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            _logger.LogWarning("Courses:ContentPath is not configured - course list will be empty");
            return null;
        }

        var root = Path.GetFullPath(configured, AppContext.BaseDirectory);
        return Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
    }

    // Wycina blok YAML między liniami '---' z początku pliku (tolerancja BOM i CRLF)
    private static string? ExtractFrontmatter(string text)
    {
        var normalized = text.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');

        if (lines.Length == 0 || lines[0].TrimEnd() != FrontmatterDelimiter)
        {
            return null;
        }

        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd() == FrontmatterDelimiter)
            {
                return string.Join('\n', lines, 1, i - 1);
            }
        }

        return null;
    }

    private sealed class FrontmatterYaml
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<string>? Tags { get; set; }
        public string? Image { get; set; }
    }
}