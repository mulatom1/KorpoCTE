using MediatR;


namespace App01.Modules.Courses.Features.Leaderboard;


public class Contracts
{
    public record Request(
        int Page = 1,
        int PageSize = 20
    ) : IRequest<Response>;

    public record Response(
        IReadOnlyList<LeaderboardEntryDto> Entries,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages
    );

    // Wpis rankingu - bez Id użytkownika i pełnego e-maila;
    // DisplayName to część e-maila przed pierwszym '@' (domena nigdy nie trafia do odpowiedzi)
    public record LeaderboardEntryDto(
        int Rank,
        string DisplayName,
        int FlagCount
    );
}