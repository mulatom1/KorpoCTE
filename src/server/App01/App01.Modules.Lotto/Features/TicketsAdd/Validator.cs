using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using Microsoft.EntityFrameworkCore;


namespace App01.Modules.Lotto.Features.TicketsAdd;


public class Validator : AbstractValidator<Contracts.Request>
{
    private readonly AppDbContext _dbContext;

    public Validator(AppDbContext dbContext)
    {
        _dbContext = dbContext;

        RuleFor(x => x.DrawTypeId)
            .GreaterThan(0)
            .WithMessage("DrawTypeId must be greater than 0");

        RuleFor(x => x.Numbers)
            .NotNull()
            .WithMessage("Numbers cannot be null")
            .NotEmpty()
            .WithMessage("Numbers cannot be empty");

        RuleFor(x => x)
            .MustAsync(async (request, cancellationToken) =>
            {
                var drawType = await _dbContext.DrawTypes
                    .FirstOrDefaultAsync(dt => dt.Id == request.DrawTypeId, cancellationToken);

                if (drawType == null)
                    return true; // DrawType existence will be checked in Handler

                // Check numbers count is within allowed range
                if (request.Numbers.Count < drawType.UserNumbersCountMin ||
                    request.Numbers.Count > drawType.UserNumbersCountMax)
                    return false;

                return true;
            })
            .WithMessage(request =>
            {
                var drawType = _dbContext.DrawTypes
                    .FirstOrDefault(dt => dt.Id == request.DrawTypeId);

                if (drawType != null)
                {
                    if (drawType.UserNumbersCountMin == drawType.UserNumbersCountMax)
                        return $"Numbers must contain exactly {drawType.UserNumbersCountMin} elements for this draw type";
                    else
                        return $"Numbers must contain between {drawType.UserNumbersCountMin} and {drawType.UserNumbersCountMax} elements for this draw type";
                }
                return "Numbers count is invalid";
            });

        RuleFor(x => x)
            .MustAsync(async (request, cancellationToken) =>
            {
                var drawType = await _dbContext.DrawTypes
                    .FirstOrDefaultAsync(dt => dt.Id == request.DrawTypeId, cancellationToken);

                if (drawType == null)
                    return true; // DrawType existence will be checked in Handler

                // Check if all numbers are within valid range
                return request.Numbers.All(n => n >= 1 && n <= drawType.NumbersMaxValue);
            })
            .WithMessage(request =>
            {
                var drawType = _dbContext.DrawTypes
                    .FirstOrDefault(dt => dt.Id == request.DrawTypeId);

                return drawType != null
                    ? $"Each number must be between 1 and {drawType.NumbersMaxValue}"
                    : "Each number must be between 1 and 49";
            });

        RuleFor(x => x.Numbers)
            .Must(numbers => numbers.Distinct().Count() == numbers.Count)
            .When(x => x.Numbers != null && x.Numbers.Count > 0)
            .WithMessage("Numbers must be unique");

        RuleFor(x => x)
            .MustAsync(async (request, cancellationToken) =>
            {
                if (request.Specials == null || request.Specials.Count == 0)
                    return true;

                var drawType = await _dbContext.DrawTypes
                    .FirstOrDefaultAsync(dt => dt.Id == request.DrawTypeId, cancellationToken);

                if (drawType == null)
                    return true; // DrawType existence will be checked in Handler

                // Check special numbers count
                if (request.Specials.Count != drawType.SpecialsCount)
                    return false;

                return true;
            })
            .When(x => x.Specials != null && x.Specials.Count > 0)
            .WithMessage(request =>
            {
                var drawType = _dbContext.DrawTypes
                    .FirstOrDefault(dt => dt.Id == request.DrawTypeId);

                return drawType != null
                    ? $"Special numbers must contain exactly {drawType.SpecialsCount} elements for this draw type"
                    : "Special numbers count mismatch";
            });

        RuleFor(x => x)
            .MustAsync(async (request, cancellationToken) =>
            {
                if (request.Specials == null || request.Specials.Count == 0)
                    return true;

                var drawType = await _dbContext.DrawTypes
                    .FirstOrDefaultAsync(dt => dt.Id == request.DrawTypeId, cancellationToken);

                if (drawType == null)
                    return true; // DrawType existence will be checked in Handler

                // Check if all special numbers are within valid range
                return request.Specials.All(n => n >= 1 && n <= drawType.SpecialsMaxValue);
            })
            .When(x => x.Specials != null && x.Specials.Count > 0)
            .WithMessage(request =>
            {
                var drawType = _dbContext.DrawTypes
                    .FirstOrDefault(dt => dt.Id == request.DrawTypeId);

                return drawType != null
                    ? $"Each special number must be between 1 and {drawType.SpecialsMaxValue}"
                    : "Special numbers out of range";
            });

        RuleFor(x => x.Specials)
            .Must(numbers => numbers == null || numbers.Distinct().Count() == numbers.Count)
            .When(x => x.Specials != null && x.Specials.Count > 0)
            .WithMessage("Special numbers must be unique");

        RuleFor(x => x.GroupName)
            .MaximumLength(100)
            .When(x => x.GroupName != null)
            .WithMessage("GroupName must not exceed 100 characters");
    }
}