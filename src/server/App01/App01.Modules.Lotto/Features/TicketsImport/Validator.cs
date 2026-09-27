using FluentValidation;


namespace App01.Modules.Lotto.Features.TicketsImport;


public class Validator : AbstractValidator<Contracts.Request>
{
    public Validator()
    {
        // Either Tickets or Csv must be provided
        RuleFor(x => x)
            .Must(x => (x.Tickets != null && x.Tickets.Count > 0) || !string.IsNullOrWhiteSpace(x.Csv))
            .WithMessage("Either Tickets list or Csv data must be provided");

        // When using Csv, DrawTypeId is required
        When(x => !string.IsNullOrWhiteSpace(x.Csv), () =>
        {
            RuleFor(x => x.DrawTypeId)
                .NotNull()
                .WithMessage("DrawTypeId is required when importing from CSV")
                .GreaterThan(0)
                .WithMessage("DrawTypeId must be greater than 0");
        });

        // Validate Tickets items when provided
        When(x => x.Tickets != null && x.Tickets.Count > 0, () =>
        {
            RuleForEach(x => x.Tickets)
                .ChildRules(ticket =>
                {
                    ticket.RuleFor(t => t.DrawTypeId)
                        .GreaterThan(0)
                        .WithMessage("DrawTypeId must be greater than 0");

                    ticket.RuleFor(t => t.Numbers)
                        .NotNull()
                        .WithMessage("Numbers list is required");
                });
        });
    }
}
