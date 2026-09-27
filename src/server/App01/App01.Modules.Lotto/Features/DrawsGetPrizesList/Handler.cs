using App01.Modules.Lotto.Services.LottoOpenApi;
using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Lotto.Features.DrawsGetPrizesList;


public class GetDrawPrizesListHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<GetDrawPrizesListHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly ILottoOpenApiService _lottoOpenApiService;
    private readonly AppDbContext _dbContext;


    public GetDrawPrizesListHandler(
        ILogger<GetDrawPrizesListHandler> logger,
        IValidator<Contracts.Request> validator,
        ILottoOpenApiService lottoOpenApiService,
        AppDbContext dbContext)
    {
        _logger = logger;
        _validator = validator;
        _lottoOpenApiService = lottoOpenApiService;
        _dbContext = dbContext;
    }

    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var drawType = await _dbContext.DrawTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(dt => dt.Id == request.DrawTypeId, cancellationToken)
            ?? throw new KeyNotFoundException($"DrawType with ID {request.DrawTypeId} not found.");

        var draw = await _dbContext.Draws
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DrawTypeId == request.DrawTypeId && d.DrawSystemId == request.DrawSystemId, cancellationToken)
            ?? throw new KeyNotFoundException($"Draw with System ID {request.DrawSystemId} not found for DrawType ID {request.DrawTypeId}.");

        var response = new Contracts.Response(new List<Contracts.WinTierDto>(), draw.DrawDate, draw.DrawSystemId, drawType.Description ?? "");

        var stats = await _lottoOpenApiService.GetDrawPrizes(drawType.Name, request.DrawSystemId);
        if (stats is null)
        {
            _logger.LogDebug("Response {@Response}", response);
            return response;
        }

        var stat = stats?.FirstOrDefault(x => x.GameType == drawType.Description);

        if (stat?.Prizes != null)
        {
            foreach (var kvp in stat.Prizes)
                response.WinTiers.Add(new Contracts.WinTierDto(kvp.Key, kvp.Value.Prize, kvp.Value.PrizeValue));
        }

        _logger.LogDebug("Response {@Response}", response);
        return response;
    }
}