using MediatR;


namespace App01.Modules.Lotto.Features.TicketsImport;


public class Contracts
{
    public record Request(
        List<TicketDto>? Tickets,
        string? Csv,
        int? DrawTypeId,
        string? GroupName
    ) : IRequest<Response>;

    public record Response(
        int ImportedCount,
        int SkippedCount,
        List<string> Errors
    );

    public record TicketDto(
        int DrawTypeId,
        string? GroupName,
        List<int> Numbers,
        List<int> Specials
    );
}