using MediatR;

namespace App01.Modules.Portal.Features.MailFromClientAdd;

public class Contracts
{
    public record Request(string Email, string Topic, string Body) : IRequest<Response>;
    public record Response(long Id);
}