using MediatR;


namespace App01.Modules.Lotto.Features.DrawsGetList;


public class Contracts
{
    public record Request(
        DateTime? DrawDateFrom,
        DateTime? DrawDateTo,
        int? DrawTypeId,
        int Page = 1,
        int PageSize = 100,
        string SortOrder = "desc"
    ) : IRequest<Response>;

    public record Response(
        List<DrawDto> Draws,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages
    );

    public record DrawDto(
        long Id,
        long DrawSystemId,
        string DrawDate,
        int DrawTypeId,
        List<int> Numbers,
        List<int> Specials
    );
}
