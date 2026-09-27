using MediatR;


namespace App01.Modules.Lotto.Features.DrawsExport;


public class Contracts
{
    public record Request(
        int DrawTypeId,
        DateTime? DrawDateFrom,
        DateTime? DrawDateTo
    ) : IRequest<Response>;

    public record Response(
        string Csv,
        string FileName,
        int TotalCount
    );
}