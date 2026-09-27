using App01.Shared.Application.Filters;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


namespace App01.Modules.Lotto.Features.TicketsGetList;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapGet("api/lotto/tickets-get-list", async (
            IMediator mediator,
            string? groupName,
            int? drawTypeId,
            int page = 1,
            int pageSize = 100) =>
        {
            var request = new Contracts.Request(groupName, drawTypeId, page, pageSize);
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("LottoGetTickets")
        .WithTags("Lotto")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization()
        .WithOpenApi();
    }
}