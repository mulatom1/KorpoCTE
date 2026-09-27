using App01.Shared.Application.Filters;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


namespace App01.Modules.Portal.Features.UserList;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapGet("api/portal/user-list", async (
            IMediator mediator,
            string? email,
            bool? isAdmin,
            int page = 1,
            int pageSize = 10) =>
        {
            var request = new Contracts.Request(email, isAdmin, page, pageSize);
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("PortalUserList")
        .WithTags("Portal")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization();
    }
}