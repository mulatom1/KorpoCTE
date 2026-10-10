using MediatR;


namespace App01.Modules.Courses.Features.HangarFlags;


public class Contracts
{
    public record Request() : IRequest<Response>;

    public record Response(
        IReadOnlyList<HangarFlagDto> Flags
    );

    // Flaga hangaru ze statusem zdobycia - bez kryteriów (są tajne);
    // Code tylko dla flagi zdobytej przez bieżącego użytkownika, dla niezdobytej null
    public record HangarFlagDto(
        int FlagId,
        string Title,
        string CourseSlug,
        bool IsEarned,
        DateTime? EarnedAt,
        string? Code
    );
}