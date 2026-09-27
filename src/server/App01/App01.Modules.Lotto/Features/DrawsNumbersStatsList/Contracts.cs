using MediatR;


namespace App01.Modules.Lotto.Features.DrawsNumbersStatsList;


public class Contracts
{
    public record Request(
        DateTime? DrawDateFrom,
        DateTime? DrawDateTo,
        long DrawTypeId,
        int NumbersGroup,
        int? SpecialsGroup,
        string SortOrder = "desc"
    ) : IRequest<Response>;

    public record Response(
        List<NumbersGroupStatsDto> NumbersStats,
        List<NumbersGroupStatsDto> SpecialsStats,
        int TotalDrawsCount,
        int TotalNumbersGroupsCount,
        int TotalSpecialsGroupsCount
    );

    public record NumbersGroupStatsDto(
        List<int> Numbers,
        int Count
    );
}