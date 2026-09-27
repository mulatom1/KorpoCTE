using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;


namespace App01.Modules.Lotto.Features.FileEdit01;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapPost("api/lotto/file-edit-01", async (
            IMediator mediator,
           [FromBody] Contracts.Request request) =>
        {
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("LottoFileEdit01")
        .WithTags("Lotto")
        .WithDescription("Edits/replace a character in a data file at a specified position.")
        .Produces(StatusCodes.Status200OK);
    }
}