using MediatR;


namespace App01.Modules.Lotto.Features.DrawsImport;


public class Contracts
{
    public record Request(
        List<DrawDto>? Draws,
        string? Csv,
        int? DrawTypeId
    ) : IRequest<Response>;

    public record Response(
        int SavedCount,
        List<string> Errors
    );

    public record DrawDto(
        long DrawSystemId,
        DateTime DrawDate,
        int DrawTypeId,
        List<int> Numbers,
        List<int> Specials
    );
}