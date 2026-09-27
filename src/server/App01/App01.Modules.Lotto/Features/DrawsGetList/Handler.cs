using App01.Shared.Infrastructure.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Lotto.Features.DrawsGetList;


public class GetListHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<GetListHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;


    public GetListHandler(
        ILogger<GetListHandler> logger,
        IValidator<Contracts.Request> validator,
        AppDbContext dbContext)
    {
        _logger = logger;
        _validator = validator;
        _dbContext = dbContext;
    }

    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var query = _dbContext.Draws
            .AsQueryable();

        if (request.DrawDateFrom.HasValue)
        {
            query = query.Where(d => d.DrawDate >= request.DrawDateFrom.Value);
        }

        if (request.DrawDateTo.HasValue)
        {
            query = query.Where(d => d.DrawDate <= request.DrawDateTo.Value);
        }

        if (request.DrawTypeId.HasValue)
        {
            query = query.Where(d => d.DrawTypeId == request.DrawTypeId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        var orderedQuery = request.SortOrder?.ToLowerInvariant() == "asc"
            ? query.OrderBy(d => d.DrawDate)
            : query.OrderByDescending(d => d.DrawDate);

        var draws = await orderedQuery
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var drawDtos = draws.Select(d => new Contracts.DrawDto(
            d.Id,
            d.DrawSystemId,
            DateTime.SpecifyKind(d.DrawDate, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            d.DrawTypeId,
            d.Numbers,
            d.Specials
        )).ToList();

        _logger.LogDebug("Retrieved {Count} draws (page {Page} of {TotalPages})", drawDtos.Count, request.Page, totalPages);

        return new Contracts.Response(drawDtos, totalCount, request.Page, request.PageSize, totalPages);
    }
}
