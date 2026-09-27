using FluentValidation;


namespace App01.Modules.Lotto.Features.DrawsExport;


public class Validator : AbstractValidator<Contracts.Request>
{
    public Validator()
    {
        RuleFor(x => x.DrawTypeId)
            .GreaterThan(0)
            .WithMessage("DrawTypeId is required and must be greater than 0");

        RuleFor(x => x.DrawDateFrom)
            .LessThanOrEqualTo(x => x.DrawDateTo)
            .When(x => x.DrawDateFrom.HasValue && x.DrawDateTo.HasValue)
            .WithMessage("DrawDateFrom must be less than or equal to DrawDateTo");
    }
}