using FluentValidation;


namespace App01.Modules.Courses.Features.Leaderboard;


public class Validator : AbstractValidator<Contracts.Request>
{
    public Validator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Numer strony musi być większy lub równy 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Rozmiar strony musi mieścić się w zakresie 1-100.");
    }
}