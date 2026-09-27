using MediatR;

namespace App01.Modules.Portal.Features.MailFromClientDelete;

public class Contracts
{
    public record Request(long Id) : IRequest<Response>;
    public record Response(bool Success);
}