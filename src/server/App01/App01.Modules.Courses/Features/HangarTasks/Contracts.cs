using MediatR;


namespace App01.Modules.Courses.Features.HangarTasks;


public class Contracts
{
    public record Request() : IRequest<Response>;

    public record Response(
        IReadOnlyList<HangarTaskDto> Tasks
    );

    // Zadanie do weryfikacji - bez kryteriów poprawności (są tajne)
    public record HangarTaskDto(
        int FlagId,
        string CourseSlug,
        string Title,
        bool IsOwned
    );
}