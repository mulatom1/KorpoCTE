using MediatR;


namespace App01.Modules.Portal.Features.UserLogin;


public class Contracts
{
    public record Request(string Email, string Password) : IRequest<Response>;

    public record Response(
        long Id,
        string Email,
        bool IsAdmin,
        DateTime CreatedAt,
        string Token,
        DateTime TokenExpiresAt
    );
}
