using App01.Shared.Application.Filters;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace App01.Modules.Portal.Features.MailFromClientList;

public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapGet("api/portal/mail-from-client-list", async (
            IMediator mediator,
            int page = 1,
            int pageSize = 10) =>
        {
            var request = new Contracts.Request(page, pageSize);
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("PortalMailFromClientList")
        .WithTags("Portal")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization()
        .WithOpenApi();
    }
}
