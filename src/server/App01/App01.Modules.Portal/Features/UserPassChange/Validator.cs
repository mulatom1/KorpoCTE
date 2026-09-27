using FluentValidation;


namespace App01.Modules.Portal.Features.UserPassChange;


public class Validator : AbstractValidator<Contracts.Request>
{
    public Validator()
    {
        RuleFor(x => x.Login)
            .NotEmpty().WithMessage("Login is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Password1)
            .NotEmpty().WithMessage("Current password is required");

        RuleFor(x => x.Password2)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(6).WithMessage("New password must be at least 6 characters");
    }
}