using MediatR;


namespace App01.Modules.Lotto.Features.DrawsDelete;


public class Contracts
{
    public record Request(
        long DrawId
    ) : IRequest<Response>;

    public record Response(
        bool Success
    );
}
