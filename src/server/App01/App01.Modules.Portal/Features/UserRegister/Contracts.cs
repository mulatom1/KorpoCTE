using MediatR;


namespace App01.Modules.Portal.Features.UserRegister;


public class Contracts
{
    public record Request(string Email, string Password) : IRequest<Response>;

    public record Response(long Id, string Email, DateTime CreatedAt);
}
