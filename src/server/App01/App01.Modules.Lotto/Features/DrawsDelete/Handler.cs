using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using App01.Shared.Application.Exceptions;
using Microsoft.Extensions.Logging;
using App01.Shared.Infrastructure.Repositories;
using App01.Shared.Application.Interfaces;


namespace App01.Modules.Lotto.Features.DrawsDelete;


public class DrawsDeleteHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<DrawsDeleteHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;


    public DrawsDeleteHandler(
        ILogger<DrawsDeleteHandler> logger,
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

        // Only admins can delete draws
        var isCallerAdmin = await _jwtService.GetIsAdminFromJwt();
        if (!isCallerAdmin)
        {
            throw new ForbiddenException("Tylko administratorzy mogą usuwać wyniki losowań");
        }

        // Find existing draw
        var draw = await _dbContext.Draws
            .FirstOrDefaultAsync(d => d.Id == request.DrawId, cancellationToken);

        if (draw == null)
        {
            throw new NotFoundException($"Wynik losowania o ID {request.DrawId} nie istnieje");
        }

        _dbContext.Draws.Remove(draw);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted draw {DrawId} (DrawSystemId: {DrawSystemId}, DrawTypeId: {DrawTypeId})",
            draw.Id, draw.DrawSystemId, draw.DrawTypeId);

        return new Contracts.Response(true);
    }
}
