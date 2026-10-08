namespace App01.Shared.Application.Entities.Courses;

public class UserFlag
{
    public int Id { get; set; }

    public required long UserId { get; set; }

    public required int FlagId { get; set; }
    public virtual Flag Flag { get; set; } = null!;

    // Data i godzina zdobycia flagi w UTC
    public required DateTime EarnedAt { get; set; }
}