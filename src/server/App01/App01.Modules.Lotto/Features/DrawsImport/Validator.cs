using FluentValidation;


namespace App01.Modules.Lotto.Features.DrawsImport;


public class Validator : AbstractValidator<Contracts.Request>
{
    public Validator()
    {
        // Either Draws or Csv must be provided
        RuleFor(x => x)
            .Must(x => (x.Draws != null && x.Draws.Count > 0) || !string.IsNullOrWhiteSpace(x.Csv))
            .WithMessage("Either Draws list or Csv data must be provided");

        // When using Csv, DrawTypeId is required
        When(x => !string.IsNullOrWhiteSpace(x.Csv), () =>
        {
            RuleFor(x => x.DrawTypeId)
                .NotNull()
                .WithMessage("DrawTypeId is required when importing from CSV")
                .GreaterThan(0)
                .WithMessage("DrawTypeId must be greater than 0");
        });

        // Validate Draws items when provided
        When(x => x.Draws != null && x.Draws.Count > 0, () =>
        {
            RuleForEach(x => x.Draws)
                .ChildRules(draw =>
                {
                    draw.RuleFor(d => d.DrawSystemId)
                        .GreaterThan(0)
                        .WithMessage("DrawSystemId must be greater than 0");

                    draw.RuleFor(d => d.DrawTypeId)
                        .GreaterThan(0)
                        .WithMessage("DrawTypeId must be greater than 0");

                    draw.RuleFor(d => d.Numbers)
                        .NotNull()
                        .WithMessage("Numbers list is required");
                });
        });
    }
}
