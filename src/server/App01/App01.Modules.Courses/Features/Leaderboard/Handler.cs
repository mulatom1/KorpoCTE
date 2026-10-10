using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Courses.Features.Leaderboard;

public class LeaderboardHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private const string EmptyDisplayName = "—";

    private readonly ILogger<LeaderboardHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;


    public LeaderboardHandler(
        ILogger<LeaderboardHandler> logger,
        IValidator<Contracts.Request> validator,
        AppDbContext dbContext,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _validator = validator;
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // Liczone tylko flagi opublikowanych kursów (UTC, równość = opublikowany);
        // agregacja po użytkowniku w bazie, potem dołączenie e-maila z Users
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var grouped = _dbContext.UserFlags
            .Where(uf => uf.Flag.Course.PublishDate <= now)
            .GroupBy(uf => uf.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                FlagCount = g.Count(),
                LastEarnedAt = g.Max(uf => uf.EarnedAt)
            });

        var rows = await grouped
            .Join(_dbContext.Users,
                g => g.UserId,
                u => u.Id,
                (g, u) => new { g.UserId, g.FlagCount, g.LastEarnedAt, u.Email })
            .ToListAsync(cancellationToken);

        // Sortowanie: więcej flag wyżej; przy remisie wyżej ten, kto wcześniej osiągnął wynik; na końcu Id (stabilność)
        var sorted = rows
            .OrderByDescending(r => r.FlagCount)
            .ThenBy(r => r.LastEarnedAt)
            .ThenBy(r => r.UserId)
            .ToList();

        // Numer miejsca = 1 + liczba osób z większą liczbą flag, liczony na pełnej liście przed paginacją
        var entries = new List<Contracts.LeaderboardEntryDto>(sorted.Count);
        var rank = 0;
        for (var i = 0; i < sorted.Count; i++)
        {
            if (i == 0 || sorted[i].FlagCount != sorted[i - 1].FlagCount)
            {
                rank = i + 1;
            }

            entries.Add(new Contracts.LeaderboardEntryDto(rank, ToDisplayName(sorted[i].Email), sorted[i].FlagCount));
        }

        var totalCount = entries.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);
        var pageEntries = entries
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        _logger.LogDebug("Leaderboard: {Total} users ranked, returned {Count} (page {Page})",
            totalCount, pageEntries.Count, request.Page);

        return new Contracts.Response(pageEntries, totalCount, request.Page, request.PageSize, totalPages);
    }

    // Część e-maila przed pierwszym '@'; bez '@' cały e-mail; pusty wynik - "—"
    private static string ToDisplayName(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return EmptyDisplayName;
        }

        var atIndex = email.IndexOf('@');
        var localPart = atIndex >= 0 ? email[..atIndex] : email;
        return string.IsNullOrEmpty(localPart) ? EmptyDisplayName : localPart;
    }
}