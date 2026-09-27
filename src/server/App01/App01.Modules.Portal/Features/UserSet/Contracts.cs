using MediatR;


namespace App01.Modules.Portal.Features.UserSet;


public class Contracts
{
    public record Request(string Email) : IRequest<Response>;

    public record Response(
        long Id,
        string Email,
        bool IsAdmin
    );
}