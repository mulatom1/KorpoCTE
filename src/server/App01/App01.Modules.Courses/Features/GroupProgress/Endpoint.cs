using App01.Shared.Application.Filters;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


namespace App01.Modules.Courses.Features.GroupProgress;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapGet("api/courses/group-progress", async (
            IMediator mediator,
            DateTime? asOf) =>
        {
            var request = new Contracts.Request(asOf);
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("CoursesGroupProgress")
        .WithTags("Courses")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization();
    }
}