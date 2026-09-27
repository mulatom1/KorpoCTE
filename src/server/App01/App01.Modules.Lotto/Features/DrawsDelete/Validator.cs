using FluentValidation;


namespace App01.Modules.Lotto.Features.DrawsDelete;


public class DrawsDeleteValidator : AbstractValidator<Contracts.Request>
{
    public DrawsDeleteValidator()
    {
        RuleFor(x => x.DrawId)
            .GreaterThan(0)
            .WithMessage("DrawId musi być większy od 0");
    }
}