using MediatR;


namespace App01.Modules.Courses.Features.HangarFlags;


public class Contracts
{
    // Filter: All | Earned | Unearned
    public record Request(
        string Filter = HangarFlagsFilter.All,
        int Page = 1,
        int PageSize = 20
    ) : IRequest<Response>;

    // TotalCount i TotalPages dotyczą listy po filtrze;
    // AllCount i EarnedCount (licznik w hangarze) nie zależą od filtra
    public record Response(
        IReadOnlyList<HangarFlagDto> Flags,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages,
        int AllCount,
        int EarnedCount
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

    public static class HangarFlagsFilter
    {
        public const string All = "All";
        public const string Earned = "Earned";
        public const string Unearned = "Unearned";
    }
}