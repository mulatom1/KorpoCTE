using MediatR;


namespace App01.Modules.Courses.Features.CourseTiles;


public class Contracts
{
    public record Request() : IRequest<Response>;

    public record Response(
        IReadOnlyList<CourseTileDto> Courses
    );

    // Wyłącznie pola publiczne kafelka - bez daty publikacji i treści kursu
    public record CourseTileDto(
        string Slug,
        string Title,
        string ShortDescription,
        IReadOnlyList<string> Tags,
        string? ImageUrl
    );
}