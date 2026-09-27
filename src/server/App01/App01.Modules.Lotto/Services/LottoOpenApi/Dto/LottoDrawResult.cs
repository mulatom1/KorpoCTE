namespace App01.Modules.Lotto.Services.LottoOpenApi.Dto;

public class LottoDrawResult
{
    public DateTime? DrawDate { get; set; }
    public long? DrawSystemId { get; set; }
    public string? GameType { get; set; }
    public List<int>? ResultsJson { get; set; }
    public List<int>? SpecialResults { get; set; }
}
