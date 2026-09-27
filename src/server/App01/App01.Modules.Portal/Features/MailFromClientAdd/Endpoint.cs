using App01.Shared.Application.Filters;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace App01.Modules.Portal.Features.MailFromClientAdd;

public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapPost("api/portal/mail-from-client-add", async (Contracts.Request request, IMediator mediator) =>
        {
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("PortalMailFromClientAdd")
        .WithTags("Portal")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .AddEndpointFilter<XTokenFilter>()
        .WithOpenApi();
    }
}