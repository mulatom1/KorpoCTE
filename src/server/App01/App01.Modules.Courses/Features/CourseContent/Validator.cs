using App01.Modules.Courses.Content;

using FluentValidation;


namespace App01.Modules.Courses.Features.CourseContent;


public class Validator : AbstractValidator<Contracts.Request>
{
    public Validator()
    {
        RuleFor(x => x.Slug)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Identyfikator kursu jest wymagany")
            .MaximumLength(100).WithMessage("Identyfikator kursu może mieć maksymalnie 100 znaków")
            .Must(CourseFrontmatterReader.IsValidSlug)
            .WithMessage("Identyfikator kursu może zawierać tylko małe litery, cyfry, '-' i '_' i musi zaczynać się od litery lub cyfry");
    }
}