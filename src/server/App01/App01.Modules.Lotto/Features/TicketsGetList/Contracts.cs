using MediatR;


namespace App01.Modules.Lotto.Features.TicketsGetList;


public class Contracts
{
    public record Request(
        string? GroupName,
        int? DrawTypeId,
        int Page = 1,
        int PageSize = 100
    ) : IRequest<Response>;

    public record Response(
        List<TicketDto> Tickets,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages
    );

    public record TicketDto(
        long Id,
        int DrawTypeId,
        string DrawTypeName,
        string? GroupName,
        DateTime CreatedAt,
        List<int> Numbers,
        List<int> Specials
    );
}