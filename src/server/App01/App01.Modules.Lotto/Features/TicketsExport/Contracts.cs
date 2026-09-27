using MediatR;


namespace App01.Modules.Lotto.Features.TicketsExport;


public class Contracts
{
    public record Request(
        string? GroupName,
        int? DrawTypeId
    ) : IRequest<Response>;

    public record Response(
        List<TicketDto> Tickets,
        string Csv,
        string FileName,
        int TotalCount,
        DateTime ExportDate
    );

    public record TicketDto(
        int DrawTypeId,
        string? GroupName,
        List<int> Numbers,
        List<int> Specials
    );
}