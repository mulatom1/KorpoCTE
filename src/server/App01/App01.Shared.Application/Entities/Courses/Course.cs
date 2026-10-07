namespace App01.Shared.Application.Entities.Courses;

public class Course
{
    public int Id { get; set; }

    // Identyfikator katalogu i pliku treści kursu: <ContentPath>/<Slug>/<Slug>.md
    public required string Slug { get; set; } = string.Empty;

    // Data i godzina publikacji w UTC
    public required DateTime PublishDate { get; set; }
}