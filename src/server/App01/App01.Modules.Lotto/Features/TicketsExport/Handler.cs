using App01.Shared.Application.Interfaces;
using App01.Shared.Infrastructure.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;


namespace App01.Modules.Lotto.Features.TicketsExport;


public class TicketsExportHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<TicketsExportHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;


    public TicketsExportHandler(
        ILogger<TicketsExportHandler> logger,
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

        var currentUserId = await _jwtService.GetUserIdFromJwt();

        var query = _dbContext.Tickets
            .Where(t => t.UserId == currentUserId);

        if (!string.IsNullOrWhiteSpace(request.GroupName))
        {
            query = query.Where(t => t.GroupName == request.GroupName);
        }

        if (request.DrawTypeId.HasValue)
        {
            query = query.Where(t => t.DrawTypeId == request.DrawTypeId.Value);
        }

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        var ticketDtos = tickets.Select(t => new Contracts.TicketDto(
            t.DrawTypeId,
            t.GroupName,
            t.Numbers,
            t.Specials
        )).ToList();

        // Generate CSV
        // Format: DrawTypeId,GroupName,n1,n2,...,s1,s2,...
        var csvBuilder = new StringBuilder();

        foreach (var ticket in ticketDtos)
        {
            var csvParts = new List<string>
            {
                ticket.DrawTypeId.ToString(),
                ticket.GroupName ?? ""
            };

            csvParts.AddRange(ticket.Numbers.Select(n => n.ToString()));
            csvParts.AddRange(ticket.Specials.Select(s => $"S:{s}"));

            csvBuilder.AppendLine(string.Join(",", csvParts));
        }

        var fileDateTime = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var fileName = request.DrawTypeId.HasValue
            ? $"Tickets_{request.DrawTypeId:D2}_{fileDateTime}.csv"
            : $"Tickets_All_{fileDateTime}.csv";

        _logger.LogInformation("Exported {Count} tickets to CSV", ticketDtos.Count);

        return new Contracts.Response(ticketDtos, csvBuilder.ToString(), fileName, ticketDtos.Count, DateTime.UtcNow);
    }
}
