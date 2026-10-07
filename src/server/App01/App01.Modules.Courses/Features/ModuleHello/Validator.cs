using FluentValidation;


namespace App01.Modules.Courses.Features.ModuleHello;


public class Validator : AbstractValidator<Contracts.Request>
{
    public Validator()
    {
        // Brak reguł - żądanie nie ma pól; walidator istnieje dla spójności wzorca wycinka
    }
}