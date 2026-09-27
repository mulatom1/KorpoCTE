using FluentValidation;


namespace App01.Modules.Lotto.Features.DrawsNumbersStatsList;


public class Validator : AbstractValidator<Contracts.Request>
{
    public Validator()
    {
        RuleFor(x => x.DrawDateFrom)
            .LessThanOrEqualTo(x => x.DrawDateTo)
            .When(x => x.DrawDateFrom.HasValue && x.DrawDateTo.HasValue)
            .WithMessage("DrawDateFrom must be less than or equal to DrawDateTo");

        RuleFor(x => x.DrawTypeId)
            .GreaterThan(0)
            .WithMessage("DrawTypeId must be greater than 0");

        RuleFor(x => x.NumbersGroup)
            .GreaterThanOrEqualTo(1)
            .WithMessage("NumbersGroup must be at least 1");
    }
}
