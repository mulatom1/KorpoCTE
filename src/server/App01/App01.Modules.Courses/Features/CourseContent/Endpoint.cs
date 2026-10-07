using App01.Shared.Application.Filters;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


namespace App01.Modules.Courses.Features.CourseContent;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        // Slug jako opcjonalny parametr query: brak parametru trafia do walidatora (400 z komunikatem),
        // a nie do wiązania parametrów minimal API
        app.MapGet("api/courses/course-content", async (string? slug, IMediator mediator) =>
        {
            var request = new Contracts.Request(slug ?? string.Empty);
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("CoursesCourseContent")
        .WithTags("Courses")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization();
    }
}