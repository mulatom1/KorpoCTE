using FluentValidation;


namespace App01.Modules.Courses.Features.ActivateFlag;


public class Validator : AbstractValidator<Contracts.Request>
{
    public const int MaxCodeLength = 50;

    public Validator()
    {
        RuleFor(x => x.Code)
            .Cascade(CascadeMode.Stop)
            .Must(code => !string.IsNullOrWhiteSpace(code)).WithMessage("Kod flagi jest wymagany")
            .MaximumLength(MaxCodeLength).WithMessage($"Kod flagi może mieć maksymalnie {MaxCodeLength} znaków");
    }
}