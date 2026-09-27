using MediatR;

namespace App01.Modules.Portal.Features.MailFromClientGet;

public class Contracts
{
    public record Request(long Id) : IRequest<Response>;

    public record Response(
        long Id,
        string Email,
        string Topic,
        string Body,
        DateTime CreatedAt
    );
}