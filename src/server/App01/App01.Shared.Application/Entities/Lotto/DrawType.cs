namespace App01.Shared.Application.Entities.Lotto;

public class DrawType
{
    public required int Id { get; set; }
    
    public required string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public required decimal TicketPrize { get; set; }

    public required int UserNumbersCountMin { get; set; }
    public required int UserNumbersCountMax { get; set; }

    public required int NumbersCount { get; set; }
    public required int NumbersMaxValue { get; set; }
    
    public required int SpecialsCount { get; set; }
    public required int SpecialsMaxValue { get; set; }

    public virtual ICollection<Draw> Draws { get; set; } = null!;
    public virtual ICollection<DrawTypeWinTier> DrawTypeWinTiers { get; set; } = null!;
}
