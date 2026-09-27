using MediatR;


namespace App01.Modules.Portal.Features.UserPassChange;


public class Contracts
{
    public record Request(string Login, string Password1, string Password2) : IRequest<Response>;

    public record Response(long Id, string Email);
}