using FluentValidation;

namespace App01.Modules.Portal.Features.MailFromClientAdd;

public class Validator : AbstractValidator<Contracts.Request>
{
    public Validator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Topic)
            .NotEmpty().WithMessage("Topic is required");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required");
    }
}