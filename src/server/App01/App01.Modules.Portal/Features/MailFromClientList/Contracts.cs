using MediatR;

namespace App01.Modules.Portal.Features.MailFromClientList;

public class Contracts
{
    public record Request(int Page, int PageSize) : IRequest<Response>;

    public record Response(
        List<MailDto> Mails,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages
    );

    public record MailDto(
        long Id,
        string Email,
        string Topic,
        DateTime CreatedAt
    );
}
