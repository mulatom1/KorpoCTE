using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


namespace App01.Modules.Portal.Features.GetApiVersion;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapGet("api/portal/get-api-version", async (IMediator mediator) =>
        {
            var request = new Contracts.Request();
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("PortalGetApiVersion")
        .WithTags("Portal")
        .Produces<Contracts.Response>(StatusCodes.Status200OK);
    }
}