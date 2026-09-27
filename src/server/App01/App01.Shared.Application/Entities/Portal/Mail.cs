namespace App01.Shared.Application.Entities.Portal;

public class Mail
{
    public required long Id { get; set; }

    public required string Email { get; set; } = string.Empty;

    public required string Topic { get; set; } = string.Empty;
    public required string Body { get; set; } = string.Empty;

    public required DateTime CreatedAt { get; set; }
}