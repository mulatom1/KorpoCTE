using MediatR;


namespace App01.Modules.Portal.Features.UserDelete;


public class Contracts
{
    public record Request(string Email) : IRequest<Response>;

    public record Response(bool Success, string Message);
}
