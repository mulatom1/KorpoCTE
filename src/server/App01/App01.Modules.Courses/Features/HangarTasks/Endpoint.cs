using App01.Shared.Application.Filters;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


namespace App01.Modules.Courses.Features.HangarTasks;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapGet("api/courses/hangar-tasks", async (IMediator mediator) =>
        {
            var request = new Contracts.Request();
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("CoursesHangarTasks")
        .WithTags("Courses")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization();
    }
}