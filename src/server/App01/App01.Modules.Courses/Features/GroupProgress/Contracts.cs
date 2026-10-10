using MediatR;


namespace App01.Modules.Courses.Features.GroupProgress;


public class Contracts
{
    // AsOf - moment, na który liczone są wskaźniki (UTC); brak = teraz, przyszłość obcinana do teraz
    public record Request(
        DateTime? AsOf = null
    ) : IRequest<Response>;

    // Wskaźniki postępu grupy na moment AsOf (zdarzenia ściśle przed AsOf);
    // EarnedPercent = null, gdy nie ma żadnej możliwej flagi
    public record Response(
        DateTime AsOf,
        int UserCount,
        int AvailableFlagCount,
        int EarnedFlagCount,
        double? EarnedPercent
    );
}