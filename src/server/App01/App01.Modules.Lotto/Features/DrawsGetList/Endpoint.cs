using App01.Shared.Application.Filters;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;


namespace App01.Modules.Lotto.Features.DrawsGetList;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapGet("api/lotto/draws-get-list", async (
            IMediator mediator,
            DateTime? drawDateFrom,
            DateTime? drawDateTo,
            int? drawTypeId,
            int page = 1,
            int pageSize = 100,
            string sortOrder = "desc") =>
        {
            var request = new Contracts.Request(drawDateFrom, drawDateTo, drawTypeId, page, pageSize, sortOrder);
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("LottoDrawsGetList")
        .WithTags("Lotto")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization();
    }
}