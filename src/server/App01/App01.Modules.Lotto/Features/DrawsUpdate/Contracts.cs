using MediatR;


namespace App01.Modules.Lotto.Features.DrawsUpdate;


public class Contracts
{
    public record Request(
        long Id,
        long DrawSystemId,
        DateTime DrawDate,
        int DrawTypeId,
        List<int> Numbers,
        List<int> Specials
    ) : IRequest<Response>;

    public record Response(
        long Id,
        DateTime UpdatedAt
    );
}
