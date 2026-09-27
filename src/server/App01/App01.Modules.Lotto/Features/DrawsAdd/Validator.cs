using FluentValidation;


namespace App01.Modules.Lotto.Features.DrawsAdd;


public class DrawsAddValidator : AbstractValidator<Contracts.Request>
{
    public DrawsAddValidator()
    {
        RuleFor(x => x.DrawSystemId)
            .GreaterThan(0)
            .WithMessage("DrawSystemId musi być większy od 0");

        RuleFor(x => x.DrawDate)
            .NotEmpty()
            .WithMessage("DrawDate jest wymagana");

        RuleFor(x => x.DrawTypeId)
            .InclusiveBetween(1, 10)
            .WithMessage("DrawTypeId musi być między 1 a 10");

        RuleFor(x => x.Numbers)
            .NotEmpty()
            .WithMessage("Numbers nie może być puste")
            .Must(numbers => numbers != null && numbers.Count > 0)
            .WithMessage("Numbers musi zawierać przynajmniej jeden numer")
            .Must(numbers => numbers != null && numbers.All(n => n > 0))
            .WithMessage("Wszystkie numery muszą być większe od 0")
            .Must(numbers => numbers == null || numbers.Distinct().Count() == numbers.Count)
            .WithMessage("Numbers nie może zawierać duplikatów");

        RuleFor(x => x.Specials)
            .Must(specials => specials == null || specials.All(n => n > 0))
            .WithMessage("Wszystkie numery specjalne muszą być większe od 0")
            .Must(specials => specials == null || specials.Distinct().Count() == specials.Count)
            .WithMessage("Specials nie może zawierać duplikatów");
    }
}
