using FluentValidation;


namespace App01.Modules.Lotto.Features.TicketsDelete;


public class Validator : AbstractValidator<Contracts.Request>
{
    public Validator()
    {
        RuleFor(x => x.TicketId)
            .GreaterThan(0)
            .WithMessage("TicketId must be greater than 0");
    }
}
