using App01.Shared.Application.Filters;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


namespace App01.Modules.Lotto.Features.DrawsGetPrizesList;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapGet("api/lotto/draws-get-prizes-list", async (
            IMediator mediator,
            int drawTypeId,
            int drawSystemId) =>
        {
            var request = new Contracts.Request(drawTypeId, drawSystemId);
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("LottoGetPrizesList")
        .WithTags("Lotto")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization()
        .WithOpenApi();
    }
}
