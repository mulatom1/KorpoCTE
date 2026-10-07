using MediatR;


namespace App01.Modules.Courses.Features.ModuleHello;


public class Contracts
{
    public record Request() : IRequest<Response>;

    public record Response(
        string Message
    );
}