using App01.Shared.Application.Entities.Lotto;

namespace App01.Shared.Application.Entities.Portal;

public class User
{
    public long Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    public DateTime CreatedAt { get; set; }


    // Lotto
    public virtual ICollection<Ticket> LottoTickets { get; set; } = [];
}