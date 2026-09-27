using App01.Shared.Application.Interfaces;
using App01.Shared.Infrastructure.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Lotto.Features.TicketsGetList;


public class GetTicketsHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<GetTicketsHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;


    public GetTicketsHandler(
        ILogger<GetTicketsHandler> logger,
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

        var query = _dbContext.Tickets
            .Include(t => t.DrawType)
            .Where(t => t.UserId == userId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.GroupName))
        {
            query = query.Where(t => t.GroupName != null && t.GroupName.Contains(request.GroupName));
        }

        if (request.DrawTypeId.HasValue)
        {
            query = query.Where(t => t.DrawTypeId == request.DrawTypeId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var ticketDtos = tickets.Select(t => new Contracts.TicketDto(
            t.Id,
            t.DrawTypeId,
            t.DrawType.Name,
            t.GroupName,
            t.CreatedAt,
            t.Numbers,
            t.Specials
        )).ToList();

        _logger.LogDebug("Retrieved {Count} tickets for user {UserId} (page {Page} of {TotalPages})",
            ticketDtos.Count, userId, request.Page, totalPages);

        return new Contracts.Response(ticketDtos, totalCount, request.Page, request.PageSize, totalPages);
    }
}
