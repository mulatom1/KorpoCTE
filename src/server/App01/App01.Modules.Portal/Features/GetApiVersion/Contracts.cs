using MediatR;


namespace App01.Modules.Portal.Features.GetApiVersion;


public class Contracts
{
    public record Request : IRequest<Response>;
    public record Response(string Version);
}