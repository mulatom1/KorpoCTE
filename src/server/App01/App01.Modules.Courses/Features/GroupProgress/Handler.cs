using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Courses.Features.GroupProgress;

public class GroupProgressHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<GroupProgressHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;


    public GroupProgressHandler(
        ILogger<GroupProgressHandler> logger,
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

        // Moment obcięty do "teraz" - przyszła data nie może ujawnić flag kursów jeszcze nieopublikowanych
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var requested = request.AsOf.HasValue ? ToUtc(request.AsOf.Value) : now;
        var asOf = requested < now ? requested : now;

        // Flagi możliwe: kursy opublikowane ściśle przed asOf
        var availableFlagCount = await _dbContext.Flags
            .Where(f => f.Course.PublishDate < asOf)
            .CountAsync(cancellationToken);

        // Zdobycia przed asOf, liczone tylko dla flag z puli możliwych (zdobyte nigdy nie przekroczą możliwych)
        var earned = _dbContext.UserFlags
            .Where(uf => uf.EarnedAt < asOf && uf.Flag.Course.PublishDate < asOf);

        var earnedFlagCount = await earned
            .Select(uf => uf.FlagId)
            .Distinct()
            .CountAsync(cancellationToken);

        // Użytkownicy z co najmniej jedną zdobytą flagą, razem z administratorami
        var userCount = await earned
            .Select(uf => uf.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        double? earnedPercent = availableFlagCount == 0
            ? null
            : Math.Round(earnedFlagCount * 100.0 / availableFlagCount, 1);

        _logger.LogDebug("Group progress as of {AsOf}: {Users} users, {Earned}/{Available} flags",
            asOf, userCount, earnedFlagCount, availableFlagCount);

        // Oznaczenie UTC, żeby JSON miał sufiks Z
        return new Contracts.Response(
            DateTime.SpecifyKind(asOf, DateTimeKind.Utc),
            userCount,
            availableFlagCount,
            earnedFlagCount,
            earnedPercent);
    }

    // Local - konwersja na UTC; Unspecified - traktowane jako UTC
    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}