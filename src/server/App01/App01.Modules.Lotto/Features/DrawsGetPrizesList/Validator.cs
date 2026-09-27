using FluentValidation;


namespace App01.Modules.Lotto.Features.DrawsGetPrizesList;


public class Validator : AbstractValidator<Contracts.Request>
{
    public Validator()
    {
        RuleFor(x => x.DrawTypeId)
            .GreaterThan(0)
            .WithMessage("DrawTypeId must be greater than 0");

        RuleFor(x => x.DrawSystemId)
            .GreaterThan(0)
            .WithMessage("DrawSystemId must be greater than 0");
    }
}