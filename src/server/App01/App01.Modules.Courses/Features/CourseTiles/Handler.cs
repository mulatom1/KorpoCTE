using System.Globalization;

using App01.Modules.Courses.Content;
using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Courses.Features.CourseTiles;

public class CourseTilesHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private const string DefaultMediaUrlBase = "/media/courses";

    // Sortowanie tytułów niezależne od kultury serwera
    private static readonly StringComparer TitleComparer =
        StringComparer.Create(CultureInfo.GetCultureInfo("pl-PL"), ignoreCase: true);

    private readonly ILogger<CourseTilesHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly ICourseFrontmatterReader _frontmatterReader;
    private readonly TimeProvider _timeProvider;
    private readonly IConfiguration _configuration;


    public CourseTilesHandler(
        ILogger<CourseTilesHandler> logger,
        IValidator<Contracts.Request> validator,
        AppDbContext dbContext,
        ICourseFrontmatterReader frontmatterReader,
        TimeProvider timeProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _validator = validator;
        _dbContext = dbContext;
        _frontmatterReader = frontmatterReader;
        _timeProvider = timeProvider;
        _configuration = configuration;
    }

    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // Filtr daty publikacji w zapytaniu EF (UTC, równość = widoczny)
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var courses = await _dbContext.Courses
            .Where(c => c.PublishDate <= now)
            .Select(c => new { c.Id, c.Slug, c.PublishDate })
            .ToListAsync(cancellationToken);

        var mediaUrlBase = GetMediaUrlBase();

        var tiles = new List<(int Id, DateTime PublishDate, Contracts.CourseTileDto Tile)>();
        foreach (var course in courses)
        {
            // Brak pliku, błędny frontmatter lub niedozwolony slug - kurs pominięty (ostrzeżenie loguje czytnik)
            var frontmatter = await _frontmatterReader.ReadAsync(course.Slug, cancellationToken);
            if (frontmatter is null)
            {
                continue;
            }

            var tile = new Contracts.CourseTileDto(
                course.Slug,
                frontmatter.Title,
                frontmatter.Description,
                frontmatter.Tags,
                BuildImageUrl(mediaUrlBase, course.Slug, frontmatter.Image));

            tiles.Add((course.Id, course.PublishDate, tile));
        }

        // Sortowanie w pamięci, bo tytuł pochodzi z pliku: data ↑, tytuł ↑, Id ↑
        var result = tiles
            .OrderBy(t => t.PublishDate)
            .ThenBy(t => t.Tile.Title, TitleComparer)
            .ThenBy(t => t.Id)
            .Select(t => t.Tile)
            .ToList();

        _logger.LogDebug("Retrieved {Count} course tiles ({Skipped} skipped)", result.Count, courses.Count - result.Count);

        return new Contracts.Response(result);
    }

    private string GetMediaUrlBase()
    {
        var configured = _configuration["Courses:MediaUrlBase"];
        var mediaUrlBase = string.IsNullOrWhiteSpace(configured) ? DefaultMediaUrlBase : configured.Trim();
        return mediaUrlBase.TrimEnd('/');
    }

    private string? BuildImageUrl(string mediaUrlBase, string slug, string? image)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            return null;
        }

        // Nazwa grafiki to sama nazwa pliku - bez separatorów i bez '..'
        if (image.Contains('/') || image.Contains('\\') || image.Contains(".."))
        {
            _logger.LogWarning("Course {Slug}: image name {Image} is not allowed - ImageUrl skipped", slug, image);
            return null;
        }

        return $"{mediaUrlBase}/{slug}/{image}";
    }
}