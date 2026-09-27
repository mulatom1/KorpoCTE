using MediatR;


namespace App01.Modules.Flashcards.Features.Generate;


public class Contracts
{
    public record Request(
        string Text,
        int Count = 5,
        string? GroupName = null
    ) : IRequest<Response>;

    public record FlashcardItem(
        string Question,
        string Answer
    );

    public record Response(
        List<FlashcardItem> Flashcards,
        int GeneratedCount,
        int InputTextLength
    );
}
