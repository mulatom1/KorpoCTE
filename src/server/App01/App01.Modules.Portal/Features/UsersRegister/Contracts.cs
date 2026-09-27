using MediatR;


namespace App01.Modules.Portal.Features.UsersRegister;


public class Contracts
{
    public record Request(List<UserToRegisterDto> Users) : IRequest<Response>;

    public record UserToRegisterDto(string Email);

    public record Response(List<RegisteredUserDto> Users);

    public record RegisteredUserDto(long Id, string Email);
}