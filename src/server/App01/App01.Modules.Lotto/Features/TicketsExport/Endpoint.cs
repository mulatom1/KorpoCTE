using App01.Shared.Application.Filters;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


namespace App01.Modules.Lotto.Features.TicketsExport;

public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapGet("api/lotto/tickets-export", async (
            IMediator mediator,
            string? groupName,
            int? drawTypeId) =>
        {
            var request = new Contracts.Request(groupName, drawTypeId);
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("LottoTicketsExport")
        .WithTags("Lotto")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization();
    }
}