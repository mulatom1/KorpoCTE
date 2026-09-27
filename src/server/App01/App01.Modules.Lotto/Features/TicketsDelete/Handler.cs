using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using App01.Shared.Application.Exceptions;
using Microsoft.Extensions.Logging;
using App01.Shared.Infrastructure.Repositories;
using App01.Shared.Application.Interfaces;


namespace App01.Modules.Lotto.Features.TicketsDelete;

public class DeleteTicketHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<DeleteTicketHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;


    public DeleteTicketHandler(
        ILogger<DeleteTicketHandler> logger,
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

        var userId = await _jwtService.GetUserIdFromJwt();

        var ticket = await _dbContext.Tickets
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket == null)
        {
            throw new NotFoundException($"Kupon o ID {request.TicketId} nie istnieje");
        }

        if (ticket.UserId != userId)
        {
            throw new ForbiddenException("Nie masz uprawnień do usunięcia tego kuponu");
        }

        _dbContext.Tickets.Remove(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted ticket {TicketId} for user {UserId}", request.TicketId, userId);

        return new Contracts.Response(true, "Kupon został usunięty");
    }
}
