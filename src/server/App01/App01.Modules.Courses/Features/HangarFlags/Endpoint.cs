using App01.Shared.Application.Filters;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


namespace App01.Modules.Courses.Features.HangarFlags;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapGet("api/courses/hangar-flags", async (
            IMediator mediator,
            string filter = Contracts.HangarFlagsFilter.All,
            int page = 1,
            int pageSize = 20) =>
        {
            var request = new Contracts.Request(filter, page, pageSize);
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("CoursesHangarFlags")
        .WithTags("Courses")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization();
    }
}