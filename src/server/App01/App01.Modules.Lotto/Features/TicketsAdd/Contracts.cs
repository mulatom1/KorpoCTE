using MediatR;


namespace App01.Modules.Lotto.Features.TicketsAdd;


public class Contracts
{
    public record Request(
        int DrawTypeId,
        string? GroupName,
        List<int> Numbers,
        List<int> Specials
    ) : IRequest<Response>;

    public record Response(
        long Id,
        DateTime CreatedAt
    );
}
