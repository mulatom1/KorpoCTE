namespace App01.Shared.Application.Entities.Courses;

public class Flag
{
    public int Id { get; set; }

    public required int CourseId { get; set; }
    public virtual Course Course { get; set; } = null!;

    // Krótki kod flagi dla administratora (unikalny)
    public required string Code { get; set; } = string.Empty;

    // Tytuł zadania widoczny dla uczestnika
    public required string Title { get; set; } = string.Empty;

    // Kryteria poprawności odpowiedzi (tajne, nigdy nie trafiają do odpowiedzi API);
    // null lub puste = flaga niezdobywalna przez weryfikację AI
    public string? Criteria { get; set; }
}