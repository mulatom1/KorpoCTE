using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using App01.Shared.Application.Exceptions;
using Microsoft.Extensions.Logging;
using App01.Shared.Infrastructure.Repositories;
using App01.Shared.Application.Interfaces;
using App01.Shared.Application.Entities.Lotto;


namespace App01.Modules.Lotto.Features.TicketsAdd;


public class AddTicketHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private const int MaxTicketsPerUser = 500;

    private readonly ILogger<AddTicketHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;


    public AddTicketHandler(
        ILogger<AddTicketHandler> logger,
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

        // Check max tickets limit
        var currentTicketCount = await _dbContext.Tickets
            .CountAsync(t => t.UserId == userId, cancellationToken);

        if (currentTicketCount >= MaxTicketsPerUser)
        {
            throw new ForbiddenException($"Osiągnięto maksymalną liczbę kuponów ({MaxTicketsPerUser})");
        }

        // Verify DrawType exists
        var drawTypeExists = await _dbContext.DrawTypes
            .AnyAsync(dt => dt.Id == request.DrawTypeId, cancellationToken);

        if (!drawTypeExists)
        {
            throw new NotFoundException($"Typ losowania o ID {request.DrawTypeId} nie istnieje");
        }

        
        var sortedNewNumbers = request.Numbers.OrderBy(n => n).ToList();
        var sortedNewSpecials = request.Specials.OrderBy(n => n).ToList();

        var existingTickets = await _dbContext.Tickets
            .Where(t => t.UserId == userId && t.DrawTypeId == request.DrawTypeId)
            .ToListAsync(cancellationToken);

        // Check for duplicate numbers
        foreach (var existingTicket in existingTickets)
        {
            var sortedExistingNumbers = existingTicket.Numbers
                .OrderBy(n => n)
                .ToList();

            var sortedExistingSpecials = existingTicket.Specials
                .OrderBy(n => n)
                .ToList();

            if (sortedNewNumbers.SequenceEqual(sortedExistingNumbers) && sortedNewSpecials.SequenceEqual(sortedExistingSpecials))
            {
                throw new ForbiddenException("Kupon z takimi numerami już istnieje");
            }
        }

        var ticket = new Ticket
        {
            Id = 0,
            UserId = userId,
            DrawTypeId = request.DrawTypeId,
            GroupName = request.GroupName,
            CreatedAt = DateTime.Now,
            Numbers = sortedNewNumbers,
            Specials = sortedNewSpecials,
        };

        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created ticket {TicketId} for user {UserId}", ticket.Id, userId);

        return new Contracts.Response(
            ticket.Id,
            ticket.CreatedAt
        );
    }
}
