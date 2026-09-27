using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using App01.Shared.Application.Exceptions;
using Microsoft.Extensions.Logging;
using App01.Shared.Infrastructure.Repositories;
using App01.Shared.Application.Interfaces;
using App01.Shared.Application.Entities.Lotto;


namespace App01.Modules.Lotto.Features.DrawsAdd;


public class DrawsAddHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<DrawsAddHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;


    public DrawsAddHandler(
        ILogger<DrawsAddHandler> logger,
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

        // Only admins can add draws
        var isCallerAdmin = await _jwtService.GetIsAdminFromJwt();
        if (!isCallerAdmin)
        {
            throw new ForbiddenException("Tylko administratorzy mogą dodawać wyniki losowań");
        }

        // Verify DrawType exists
        var drawTypeExists = await _dbContext.DrawTypes
            .AnyAsync(dt => dt.Id == request.DrawTypeId, cancellationToken);

        if (!drawTypeExists)
        {
            throw new NotFoundException($"Typ losowania o ID {request.DrawTypeId} nie istnieje");
        }

        // Check if draw with same DrawSystemId and DrawTypeId already exists
        var existingDraw = await _dbContext.Draws
            .AnyAsync(d => d.DrawSystemId == request.DrawSystemId && d.DrawTypeId == request.DrawTypeId, cancellationToken);

        if (existingDraw)
        {
            throw new ForbiddenException($"Wynik losowania o numerze systemowym {request.DrawSystemId} dla typu {request.DrawTypeId} już istnieje");
        }

        var sortedNumbers = request.Numbers.OrderBy(n => n).ToList();
        var sortedSpecials = request.Specials.OrderBy(n => n).ToList();

        var draw = new Draw
        {
            Id = 0,
            DrawSystemId = request.DrawSystemId,
            DrawDate = request.DrawDate.ToUniversalTime(),
            DrawTypeId = request.DrawTypeId,
            Numbers = sortedNumbers,
            Specials = sortedSpecials,
        };

        _dbContext.Draws.Add(draw);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created draw {DrawId} (DrawSystemId: {DrawSystemId}, DrawTypeId: {DrawTypeId})",
            draw.Id, draw.DrawSystemId, draw.DrawTypeId);

        return new Contracts.Response(
            draw.Id,
            draw.DrawDate
        );
    }
}
