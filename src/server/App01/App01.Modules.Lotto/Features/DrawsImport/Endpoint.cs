using App01.Shared.Application.Filters;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


namespace App01.Modules.Lotto.Features.DrawsImport;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapPost("api/lotto/draws-import", async (
            IMediator mediator,
            Contracts.Request request) =>
        {
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("LottoDrawsImport")
        .WithTags("Lotto")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization()
        .WithOpenApi();
    }
}