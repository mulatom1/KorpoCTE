namespace App01.Modules.Lotto.Services.LottoOpenApi.Dto;

public class LottoDraw
{
    public long? DrawSystemId { get; set; }
    public DateTime? DrawDate { get; set; }
    public string? GameType { get; set; }
    public List<LottoDrawResult>? Results { get; set; }
    public bool? ShowSpecialResults { get; set; }
    public bool? IsNewEuroJackpotDraw { get; set; }
}
