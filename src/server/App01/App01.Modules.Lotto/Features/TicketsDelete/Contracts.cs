using MediatR;


namespace App01.Modules.Lotto.Features.TicketsDelete;


public class Contracts
{
    public record Request(
        long TicketId
    ) : IRequest<Response>;

    public record Response(
        bool Success,
        string Message
    );
}