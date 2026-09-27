using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using App01.Shared.Application.Exceptions;
using Microsoft.Extensions.Logging;
using App01.Shared.Infrastructure.Repositories;
using App01.Shared.Application.Interfaces;


namespace App01.Modules.Lotto.Features.DrawsUpdate;


public class DrawsUpdateHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<DrawsUpdateHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;


    public DrawsUpdateHandler(
        ILogger<DrawsUpdateHandler> logger,
        IValidator<Contracts.Request> validator,
        AppDbContext dbContext,
        IJwtService jwtService)
    {
        _logger = logger;
        _validator = validator;
        _dbContext = dbContext;
        _jwtService = jwtService;
    }

    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // Only admins can update draws
        var isCallerAdmin = await _jwtService.GetIsAdminFromJwt();
        if (!isCallerAdmin)
        {
            throw new ForbiddenException("Tylko administratorzy mogą edytować wyniki losowań");
        }

        // Find existing draw
        var draw = await _dbContext.Draws
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        if (draw == null)
        {
            throw new NotFoundException($"Wynik losowania o ID {request.Id} nie istnieje");
        }

        // Verify DrawType exists
        var drawTypeExists = await _dbContext.DrawTypes
            .AnyAsync(dt => dt.Id == request.DrawTypeId, cancellationToken);

        if (!drawTypeExists)
        {
            throw new NotFoundException($"Typ losowania o ID {request.DrawTypeId} nie istnieje");
        }

        // Check if another draw with same DrawSystemId and DrawTypeId exists (excluding current draw)
        var conflictingDraw = await _dbContext.Draws
            .AnyAsync(d => d.DrawSystemId == request.DrawSystemId
                && d.DrawTypeId == request.DrawTypeId
                && d.Id != request.Id, cancellationToken);

        if (conflictingDraw)
        {
            throw new ForbiddenException($"Inny wynik losowania o numerze systemowym {request.DrawSystemId} dla typu {request.DrawTypeId} już istnieje");
        }

        var sortedNumbers = request.Numbers.OrderBy(n => n).ToList();
        var sortedSpecials = request.Specials.OrderBy(n => n).ToList();

        // Update draw
        draw.DrawSystemId = request.DrawSystemId;
        draw.DrawDate = request.DrawDate.ToUniversalTime();
        draw.DrawTypeId = request.DrawTypeId;
        draw.Numbers = sortedNumbers;
        draw.Specials = sortedSpecials;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated draw {DrawId} (DrawSystemId: {DrawSystemId}, DrawTypeId: {DrawTypeId})",
            draw.Id, draw.DrawSystemId, draw.DrawTypeId);

        return new Contracts.Response(
            draw.Id,
            draw.DrawDate
        );
    }
}
