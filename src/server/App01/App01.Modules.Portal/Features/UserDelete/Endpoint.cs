using App01.Shared.Application.Filters;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


namespace App01.Modules.Portal.Features.UserDelete;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapDelete("api/portal/user-delete", async (string email, IMediator mediator) =>
        {
            var request = new Contracts.Request(email);
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("PortalUserDelete")
        .WithTags("Portal")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization();
    }
}