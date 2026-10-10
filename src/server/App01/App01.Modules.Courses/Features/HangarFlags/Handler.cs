using App01.Shared.Application.Interfaces;
using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Courses.Features.HangarFlags;

public class HangarFlagsHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<HangarFlagsHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;
    private readonly TimeProvider _timeProvider;


    public HangarFlagsHandler(
        ILogger<HangarFlagsHandler> logger,
        IValidator<Contracts.Request> validator,
        AppDbContext dbContext,
        IJwtService jwtService,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _validator = validator;
        _dbContext = dbContext;
        _jwtService = jwtService;
        _timeProvider = timeProvider;
    }

    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // Id bieżącego użytkownika wyłącznie z JWT
        var userId = await _jwtService.GetUserIdFromJwt();

        // Wszystkie flagi opublikowanych kursów (UTC, równość = widoczny), także bez kryteriów;
        // data zdobycia tylko dla bieżącego użytkownika (podzapytanie do UserFlags)
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var rows = await _dbContext.Flags
            .Where(f => f.Course.PublishDate <= now)
            .Select(f => new
            {
                FlagId = f.Id,
                f.Title,
                CourseSlug = f.Course.Slug,
                CoursePublishDate = f.Course.PublishDate,
                f.Code,
                EarnedAt = _dbContext.UserFlags
                    .Where(uf => uf.FlagId == f.Id && uf.UserId == userId)
                    .Select(uf => (DateTime?)uf.EarnedAt)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // Sortowanie w pamięci: zdobyte (najnowsze najpierw), potem niezdobyte wg daty publikacji kursu
        var flags = rows
            .OrderBy(r => r.EarnedAt.HasValue ? 0 : 1)
            .ThenByDescending(r => r.EarnedAt)
            .ThenBy(r => r.EarnedAt.HasValue ? DateTime.MinValue : r.CoursePublishDate)
            .ThenBy(r => r.FlagId)
            .Select(r => new Contracts.HangarFlagDto(
                r.FlagId,
                r.Title,
                r.CourseSlug,
                r.EarnedAt.HasValue,
                // EF czyta datetime2 jako Unspecified - oznaczamy UTC, żeby JSON miał sufiks Z
                r.EarnedAt.HasValue ? DateTime.SpecifyKind(r.EarnedAt.Value, DateTimeKind.Utc) : null,
                // Kod aktywacji wyłącznie dla flagi już zdobytej przez bieżącego użytkownika
                r.EarnedAt.HasValue ? r.Code : null))
            .ToList();

        _logger.LogDebug("Retrieved {Count} hangar flags for user {UserId}", flags.Count, userId);

        return new Contracts.Response(flags);
    }
}