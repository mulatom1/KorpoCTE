using App01.Shared.Application.Filters;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


namespace App01.Modules.Lotto.Features.TicketsDelete;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapDelete("api/lotto/tickets-delete", async (
            IMediator mediator,
            long ticketId) =>
        {
            var request = new Contracts.Request(ticketId);
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("LottoDeleteTicket")
        .WithTags("Lotto")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization()
        .WithOpenApi();
    }
}
