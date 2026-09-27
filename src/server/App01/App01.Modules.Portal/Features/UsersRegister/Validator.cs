using FluentValidation;


namespace App01.Modules.Portal.Features.UsersRegister;


public class Validator : AbstractValidator<Contracts.Request>
{
    public Validator()
    {
        RuleFor(x => x.Users)
            .NotNull().WithMessage("Users are required")
            .Must(users => users != null && users.Count > 0).WithMessage("At least one user is required")
            .Must(users => users == null || users.Count <= 100).WithMessage("Maximum 100 users can be registered at once");

        RuleForEach(x => x.Users).ChildRules(user =>
        {
            user.RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format");
        });

        RuleFor(x => x.Users)
            .Must(users => users == null
                || users.Select(u => u.Email?.Trim().ToLowerInvariant()).Distinct().Count() == users.Count)
            .WithMessage("Duplicated emails in request");
    }
}
