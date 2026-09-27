using MediatR;


namespace App01.Modules.Lotto.Features.DrawsGetPrizesList;

public class Contracts
{
    public record Request(
        int DrawTypeId,
        long DrawSystemId
    ) : IRequest<Response>;

    public record Response(
        List<WinTierDto> WinTiers,
        DateTime DrawDate,
        long DrawSystemId,
        string GameType
    );

    public record WinTierDto(
       string Tier,
       int WinsCount,
       decimal WinsPrize
    );
}