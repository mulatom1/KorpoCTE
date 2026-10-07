using App01.Shared.Application.Filters;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


namespace App01.Modules.Courses.Features.CourseTiles;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        // Endpoint publiczny: tylko X-TOKEN, bez RequireAuthorization()
        app.MapGet("api/courses/course-tiles", async (IMediator mediator) =>
        {
            var request = new Contracts.Request();
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("CoursesCourseTiles")
        .WithTags("Courses")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .AddEndpointFilter<XTokenFilter>();
    }
}