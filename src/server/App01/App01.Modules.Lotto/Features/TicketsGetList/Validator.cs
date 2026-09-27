using FluentValidation;


namespace App01.Modules.Lotto.Features.TicketsGetList;


public class Validator : AbstractValidator<Contracts.Request>
{
    public Validator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageSize must be at least 1")
            .LessThanOrEqualTo(1000)
            .WithMessage("PageSize must not exceed 1000");
    }
}
