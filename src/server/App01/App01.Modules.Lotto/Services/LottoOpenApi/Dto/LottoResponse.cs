namespace App01.Modules.Lotto.Services.LottoOpenApi.Dto;

public class LottoResponse
{
    public bool Status { get; set; }
    public string? Code { get; set; } = null;
    public string? Content { get; set; } = null;
    public string? Error { get; set; } = null;
}
