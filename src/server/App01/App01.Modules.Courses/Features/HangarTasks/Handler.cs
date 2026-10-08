using App01.Shared.Application.Interfaces;
using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Courses.Features.HangarTasks;

public class HangarTasksHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<HangarTasksHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;
    private readonly TimeProvider _timeProvider;


    public HangarTasksHandler(
        ILogger<HangarTasksHandler> logger,
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

        // Tylko flagi z kryteriami (zdobywalne przez weryfikację) w opublikowanych kursach (UTC, równość = widoczny)
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var tasks = await _dbContext.Flags
            .Where(f => f.Criteria != null && f.Criteria != "" && f.Course.PublishDate <= now)
            .OrderBy(f => f.Course.PublishDate)
            .ThenBy(f => f.Id)
            .Select(f => new Contracts.HangarTaskDto(
                f.Id,
                f.Course.Slug,
                f.Title,
                _dbContext.UserFlags.Any(uf => uf.FlagId == f.Id && uf.UserId == userId)))
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} hangar tasks for user {UserId}", tasks.Count, userId);

        return new Contracts.Response(tasks);
    }
}