using App01.Shared.Application.Models.AI;

namespace App01.Shared.Application.Interfaces;

public interface IOpenRouterService
{
    Task<string> ChatAsync(string? model, IList<ChatMessage> historyMessages, ChatMessage prompt, CancellationToken cancellationToken = default);
}