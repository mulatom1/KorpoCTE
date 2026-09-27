using MediatR;


namespace App01.Modules.Lotto.Features.FileEdit01;


public class Contracts
{
    public record Request(string FileName, int Position, char NewChar) : IRequest<Response>;

    public record Response(bool IsSuccess, string Message);
}