namespace App01.Modules.Lotto.Dto;


public class DrawWinTier
{
    public required int WinTier { get; set; }
    
    public required int WinCount { get; set; }

    public required decimal WinAmount { get; set; }
}
