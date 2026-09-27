using MediatR;


namespace App01.Modules.Portal.Features.UserList;


public class Contracts
{
    public record Request(
        string? Email,
        bool? IsAdmin,
        int Page,
        int PageSize
    ) : IRequest<Response>;

    public record Response(
        List<UserDto> Users,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages
    );

    public record UserDto(
        long Id,
        string Email,
        bool IsAdmin,
        DateTime CreatedAt
    );
}