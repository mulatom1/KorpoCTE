using FluentValidation;


namespace App01.Modules.Portal.Features.UserLogin;


public class Validator : AbstractValidator<Contracts.Request>
{
    public Validator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}
