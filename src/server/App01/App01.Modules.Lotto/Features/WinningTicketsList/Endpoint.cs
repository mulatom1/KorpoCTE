using App01.Shared.Application.Filters;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


namespace App01.Modules.Lotto.Features.WinningTicketsList;


public static class Endpoint
{
    public static void AddEndpoint(this WebApplication app)
    {
        app.MapGet("api/lotto/winning-tickets-list", async (
            IMediator mediator,
            DateTime? drawDateFrom,
            DateTime? drawDateTo,
            int? drawTypeId,
            string? groupName,
            int page = 1,
            int pageSize = 100,
            bool winTier1 = true,
            bool winTier2 = true,
            bool winTier3 = true,
            bool winTier4 = true,
            bool winTier5 = true,
            bool winTier6 = true,
            bool winTier7 = true,
            bool winTier8 = true,
            bool winTier9 = true,
            bool winTier10 = true,
            bool winTier11 = true,
            bool winTier12 = true,
            bool hideDrawsWithoutMatches = false) =>
        {
            var request = new Contracts.Request(
                drawDateFrom, drawDateTo, drawTypeId, groupName, page, pageSize,
                winTier1, winTier2, winTier3, winTier4, winTier5, winTier6,
                winTier7, winTier8, winTier9, winTier10, winTier11, winTier12,
                hideDrawsWithoutMatches);
            var result = await mediator.Send(request);
            return Results.Ok(result);
        })
        .WithName("LottoWinningTicketsList")
        .WithTags("Lotto")
        .Produces<Contracts.Response>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .AddEndpointFilter<XTokenFilter>()
        .RequireAuthorization()
        .WithOpenApi();
    }
}
