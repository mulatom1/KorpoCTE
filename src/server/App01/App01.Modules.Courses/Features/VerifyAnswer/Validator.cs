using FluentValidation;


namespace App01.Modules.Courses.Features.VerifyAnswer;


public class Validator : AbstractValidator<Contracts.Request>
{
    public const int MaxAnswerLength = 4000;

    public Validator()
    {
        RuleFor(x => x.FlagId)
            .GreaterThan(0).WithMessage("Identyfikator zadania musi być większy od 0");

        RuleFor(x => x.Answer)
            .Cascade(CascadeMode.Stop)
            .Must(answer => !string.IsNullOrWhiteSpace(answer)).WithMessage("Odpowiedź jest wymagana")
            .MaximumLength(MaxAnswerLength).WithMessage($"Odpowiedź może mieć maksymalnie {MaxAnswerLength} znaków");
    }
}