using App01.Shared.Application.Filters;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace App01.Modules.Portal.Features.MailFromClientGet;

public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapGet("api/portal/mail-from-client-get", async (
            IMediator mediator,
            long id) =>
        {
            var request = new Contracts.Request(id);
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("PortalMailFromClientGet")
        .WithTags("Portal")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization()
        .WithOpenApi();
    }
}
