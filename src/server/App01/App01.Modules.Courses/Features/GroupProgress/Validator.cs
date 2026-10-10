using FluentValidation;


namespace App01.Modules.Courses.Features.GroupProgress;


public class Validator : AbstractValidator<Contracts.Request>
{
    private static readonly DateTime MinAsOf = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public Validator()
    {
        // Daty z przyszłości nie są błędem - handler obcina je do "teraz"
        RuleFor(x => x.AsOf)
            .GreaterThanOrEqualTo(MinAsOf)
            .When(x => x.AsOf.HasValue)
            .WithMessage("Data musi być późniejsza niż 2000-01-01");
    }
}