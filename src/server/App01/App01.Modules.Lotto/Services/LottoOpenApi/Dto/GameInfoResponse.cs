namespace App01.Modules.Lotto.Services.LottoOpenApi.Dto;


public class GameInfoResponse
{
    public string GameType { get; set; } = null!;

    public DateTime NextDrawDate { get; set; }

    public decimal ClosestPrizeValue { get; set; }

    public string Draws { get; set; } = null!;

    public string CouponPrice { get; set; } = null!;

    public string? ClosestPrizePoolType { get; set; } = null!;
}