using MediatR;


namespace App01.Modules.Courses.Features.ActivateFlag;


public class Contracts
{
    public record Request(
        string Code
    ) : IRequest<Response>;

    // Message to stały tekst dla statusu. Odpowiedź nigdy nie zawiera kodu flagi.
    // FlagTitle jest wypełniony dla statusów Activated i AlreadyOwned, dla Invalid ma wartość null.
    public record Response(
        string Status,
        string Message,
        string? FlagTitle = null
    );

    public static class Statuses
    {
        public const string Activated = "Activated";
        public const string AlreadyOwned = "AlreadyOwned";
        public const string Invalid = "Invalid";
    }
}