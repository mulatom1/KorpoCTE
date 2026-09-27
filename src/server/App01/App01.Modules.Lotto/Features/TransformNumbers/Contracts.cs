using MediatR;


namespace App01.Modules.Lotto.Features.TransformNumbers;


public class Contracts
{
    public record Request(
        List<int> Numbers,
        List<int> Specials,
        long NumbersLow,
        long NumbersHigh,
        long SpecialsLow,
        long SpecialsHigh
    ) : IRequest<Response>;

    public record Response(
        long NumbersLow,
        long NumbersHigh,
        long SpecialsLow,
        long SpecialsHigh,
        List<int> Numbers,
        List<int> Specials
    );
}