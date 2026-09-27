using FluentValidation;


namespace App01.Modules.Lotto.Features.FileEdit01;


public class Validator : AbstractValidator<Contracts.Request>
{
    public Validator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithMessage("FileName must not be empty");
        RuleFor(x => x.Position)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Position must be non-negative");
        RuleFor(x => x.NewChar)
            .NotEmpty()
            .WithMessage("NewChar must not be empty");
    }
}