using App01.Shared.Application.Entities.Courses;
using App01.Shared.Application.Interfaces;
using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Courses.Features.ActivateFlag;

public class ActivateFlagHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private const string MessageActivated = "Flaga aktywowana!";
    private const string MessageAlreadyOwned = "Masz już tę flagę.";
    private const string MessageInvalid = "Nieprawidłowy kod flagi.";

    private readonly ILogger<ActivateFlagHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;
    private readonly TimeProvider _timeProvider;


    public ActivateFlagHandler(
        ILogger<ActivateFlagHandler> logger,
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
        // 1. Walidacja
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // 2. Id bieżącego użytkownika wyłącznie z JWT
        var userId = await _jwtService.GetUserIdFromJwt();

        // 3. Flaga z opublikowanego kursu po kodzie (UTC, równość = widoczny), także bez kryteriów.
        // Porównanie bez rozróżniania wielkości liter przez ToUpper po obu stronach -
        // nie polegamy na kolacji bazy (InMemory rozróżnia wielkość liter).
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var flag = await _dbContext.Flags
            .Where(f => f.Code.ToUpper() == normalizedCode && f.Course.PublishDate <= now)
            .Select(f => new { f.Id, f.Title })
            .FirstOrDefaultAsync(cancellationToken);

        // 4. Nieznany kod lub kurs nieopublikowany - bez zapisywania wpisanego kodu w logu
        if (flag == null)
        {
            _logger.LogWarning("Invalid flag activation attempt by user {UserId}", userId);
            return new Contracts.Response(Contracts.Statuses.Invalid, MessageInvalid);
        }

        // 5. Flaga już zdobyta przez bieżącego użytkownika
        var alreadyOwned = await _dbContext.UserFlags
            .AnyAsync(uf => uf.UserId == userId && uf.FlagId == flag.Id, cancellationToken);
        if (alreadyOwned)
        {
            return new Contracts.Response(Contracts.Statuses.AlreadyOwned, MessageAlreadyOwned, flag.Title);
        }

        // 6. Zapis flagi - unikalny indeks (UserId, FlagId) odrzuca równoczesny drugi zapis
        var userFlag = new UserFlag
        {
            UserId = userId,
            FlagId = flag.Id,
            EarnedAt = now
        };
        _dbContext.UserFlags.Add(userFlag);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // AlreadyOwned tylko wtedy, gdy wiersz faktycznie zapisał równoległy request;
            // inny błąd zapisu (np. klucz obcy) idzie dalej do middleware
            _dbContext.Entry(userFlag).State = EntityState.Detached;
            var savedConcurrently = await _dbContext.UserFlags
                .AnyAsync(uf => uf.UserId == userId && uf.FlagId == flag.Id, cancellationToken);
            if (!savedConcurrently)
            {
                throw;
            }

            _logger.LogWarning(ex, "Concurrent activation of flag {FlagId} by user {UserId} - already owned", flag.Id, userId);
            return new Contracts.Response(Contracts.Statuses.AlreadyOwned, MessageAlreadyOwned, flag.Title);
        }

        // 7. Flaga aktywowana
        _logger.LogInformation("Flag {FlagId} activated by user {UserId}", flag.Id, userId);

        return new Contracts.Response(Contracts.Statuses.Activated, MessageActivated, flag.Title);
    }
}