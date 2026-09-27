using App01.Shared.Application.Filters;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


namespace App01.Modules.Lotto.Features.DrawsNumbersStatsList;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapGet("api/lotto/draws-numbers-stats-list", async (
            IMediator mediator,
            DateTime? drawDateFrom,
            DateTime? drawDateTo,
            long drawTypeId,
            int numbersGroup,
            int? specialsGroup,
            string sortOrder = "desc") =>
        {
            var request = new Contracts.Request(drawDateFrom, drawDateTo, drawTypeId, numbersGroup, specialsGroup, sortOrder);
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("LottoDrawsNumbersStatsList")
        .WithTags("Lotto")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization()
        .WithOpenApi();
    }
}