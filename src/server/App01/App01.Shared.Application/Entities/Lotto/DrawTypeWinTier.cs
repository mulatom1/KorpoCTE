namespace App01.Shared.Application.Entities.Lotto;

public class DrawTypeWinTier
{
    public required int Id { get; set; }

    public required int DrawTypeId { get; set; }
    public virtual DrawType DrawType { get; set; } = null!;

    public required int WinTier { get; set; }

    public required int NumbersMatchCount { get; set; }
    public required int NumbersMatchSelected { get; set; }

    public required int SpecialsMatchCount { get; set; }
    public required int SpecialsMatchSelected { get; set; }

    public decimal PotentialWinPrize { get; set; } = 0m;
}