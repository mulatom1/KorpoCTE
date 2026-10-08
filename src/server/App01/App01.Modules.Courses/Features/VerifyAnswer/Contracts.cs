using MediatR;


namespace App01.Modules.Courses.Features.VerifyAnswer;


public class Contracts
{
    public record Request(
        int FlagId,
        string Answer
    ) : IRequest<Response>;

    // Message to stały tekst dla statusu - nigdy nie zawiera uzasadnienia modelu ani kryteriów.
    // Code (kod flagi do aktywacji) jest wypełniony wyłącznie dla statusu Correct.
    public record Response(
        string Status,
        string Message,
        string? Code = null
    );

    public static class Statuses
    {
        public const string Correct = "Correct";
        public const string Incorrect = "Incorrect";
        public const string Unavailable = "Unavailable";
        public const string AlreadyOwned = "AlreadyOwned";
    }
}