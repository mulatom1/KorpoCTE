using FluentValidation;


namespace App01.Modules.Flashcards.Features.Generate;


public class DrawsAddValidator : AbstractValidator<Contracts.Request>
{
    public DrawsAddValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage("Text jest wymagany")
            .Must(text => text != null && text.Length > 200)
            .WithMessage("Text musi zawierać przynajmniej 200 znaków");

        RuleFor(x => x.Count)
            .GreaterThan(0)
            .WithMessage("Count musi być większy niż 0")
            .LessThanOrEqualTo(20)
            .WithMessage("Count nie może być większy niż 20");
    }
}