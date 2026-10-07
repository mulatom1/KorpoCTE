using App01.Modules.Courses.Content;
using App01.Shared.Application.Exceptions;
using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Courses.Features.CourseContent;

public class CourseContentHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private const string DefaultMediaUrlBase = "/media/courses";

    // Jeden komunikat dla każdego powodu niedostępności - nie zdradza istnienia nieopublikowanych kursów
    private const string NotFoundMessage = "Kurs nie istnieje lub nie jest jeszcze opublikowany";

    private readonly ILogger<CourseContentHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly ICourseFrontmatterReader _frontmatterReader;
    private readonly TimeProvider _timeProvider;
    private readonly IConfiguration _configuration;


    public CourseContentHandler(
        ILogger<CourseContentHandler> logger,
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
        var course = await _dbContext.Courses
            .Where(c => c.Slug == request.Slug && c.PublishDate <= now)
            .Select(c => new { c.Id, c.Slug })
            .FirstOrDefaultAsync(cancellationToken);

        if (course is null)
        {
            _logger.LogDebug("Course {Slug} not found or not published yet", request.Slug);
            throw new NotFoundException(NotFoundMessage);
        }

        // Ścieżka pliku budowana ze sluga z encji, nie z parametru requestu
        var document = await _frontmatterReader.ReadAsync(course.Slug, cancellationToken);
        if (document is null)
        {
            // Powód (brak pliku, błędny frontmatter, bezpiecznik wwwroot) loguje czytnik
            throw new NotFoundException(NotFoundMessage);
        }

        var mediaBaseUrl = $"{GetMediaUrlBase()}/{course.Slug}";

        _logger.LogDebug("Retrieved content of course {Slug}", course.Slug);

        return new Contracts.Response(course.Slug, document.Title, document.Tags, document.Body, mediaBaseUrl);
    }

    private string GetMediaUrlBase()
    {
        var configured = _configuration["Courses:MediaUrlBase"];
        var mediaUrlBase = string.IsNullOrWhiteSpace(configured) ? DefaultMediaUrlBase : configured.Trim();
        return mediaUrlBase.TrimEnd('/');
    }
}