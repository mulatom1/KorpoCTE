using System.Text;

using App01.Shared.Application.Exceptions;
using App01.Shared.Application.Interfaces;
using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Lotto.Features.DrawsExport;

public class DrawsExportHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<DrawsExportHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public DrawsExportHandler(
        ILogger<DrawsExportHandler> logger,
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

        var isCallerAdmin = await _jwtService.GetIsAdminFromJwt();
        if (!isCallerAdmin)
        {
            throw new ForbiddenException("Only administrators can export draws");
        }

        var drawType = await _dbContext.DrawTypes
            .FirstOrDefaultAsync(dt => dt.Id == request.DrawTypeId, cancellationToken)
            ?? throw new NotFoundException($"DrawType with id {request.DrawTypeId} not found");

        var query = _dbContext.Draws
            .Where(d => d.DrawTypeId == request.DrawTypeId);

        if (request.DrawDateFrom.HasValue)
        {
            query = query.Where(d => d.DrawDate >= request.DrawDateFrom.Value);
        }

        if (request.DrawDateTo.HasValue)
        {
            query = query.Where(d => d.DrawDate <= request.DrawDateTo.Value);
        }

        var draws = await query
            .OrderByDescending(d => d.DrawDate)
            .ToListAsync(cancellationToken);

        // Generate CSV compatible with Worker03
        // Format: DrawSystemId(20),DrawDate(UTC),n1,n2,...,s1,s2,...
        var csvBuilder = new StringBuilder();

        foreach (var draw in draws)
        {
            var specials = request.DrawTypeId == 8
                ? draw.Numbers  // Kaskada - same as numbers
                : draw.Specials;

            var csvParts = new List<string>
            {
                draw.DrawSystemId.ToString("D20"),
                draw.DrawDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            csvParts.AddRange(draw.Numbers.Select(n => n.ToString()));
            csvParts.AddRange(specials.Select(s => s.ToString()));

            csvBuilder.AppendLine(string.Join(",", csvParts));
        }

        var fileName = $"Data_{request.DrawTypeId:D2}_{drawType.Name}.csv";

        _logger.LogInformation("Exported {Count} draws for DrawTypeId {DrawTypeId}", draws.Count, request.DrawTypeId);

        return new Contracts.Response(csvBuilder.ToString(), fileName, draws.Count);
    }
}