using MediatR;


namespace App01.Modules.Lotto.Features.WinningTicketsList;


public class Contracts
{
    public record Request(
        DateTime? DrawDateFrom,
        DateTime? DrawDateTo,
        int? DrawTypeId,
        string? GroupName,
        int Page = 1,
        int PageSize = 100,
        bool WinTier1 = true,
        bool WinTier2 = true,
        bool WinTier3 = true,
        bool WinTier4 = true,
        bool WinTier5 = true,
        bool WinTier6 = true,
        bool WinTier7 = true,
        bool WinTier8 = true,
        bool WinTier9 = true,
        bool WinTier10 = true,
        bool WinTier11 = true,
        bool WinTier12 = true,
        bool HideDrawsWithoutMatches = false
    ) : IRequest<Response>
    {
        public HashSet<int> GetEnabledWinTiers()
        {
            var tiers = new HashSet<int>();
            if (WinTier1) tiers.Add(1);
            if (WinTier2) tiers.Add(2);
            if (WinTier3) tiers.Add(3);
            if (WinTier4) tiers.Add(4);
            if (WinTier5) tiers.Add(5);
            if (WinTier6) tiers.Add(6);
            if (WinTier7) tiers.Add(7);
            if (WinTier8) tiers.Add(8);
            if (WinTier9) tiers.Add(9);
            if (WinTier10) tiers.Add(10);
            if (WinTier11) tiers.Add(11);
            if (WinTier12) tiers.Add(12);
            return tiers;
        }
    }

    public record Response(
        List<DrawWithTicketsDto> Draws,
        SummaryDto Summary,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages
    );

    public record DrawWithTicketsDto(
        long Id,
        long DrawSystemId,
        string DrawDate,
        int DrawTypeId,
        decimal TicketPrice,
        List<int> Numbers,
        List<int> Specials,
        List<MatchingTicketDto> MatchingTickets
    );

    public record MatchingTicketDto(
        long Id,
        int DrawTypeId,
        string? GroupName,
        DateTime CreatedAt,
        List<int> Numbers,
        List<int> Specials,
        List<int> MatchedNumbers,
        List<int> MatchedSpecials,
        int WinTier,
        decimal WinPrize,
        decimal TicketPrice
    );

    public record SummaryDto(
        int TotalDraws,              // Ilość losowań w okresie
        int TotalTickets,            // Ilość szablonów kuponów użytkownika
        int TotalBets,               // Suma zakładów = Σ(ilość losowań × ilość kuponów) per typ
        decimal TotalCost,           // Suma kosztów = Σ(ilość losowań × ilość kuponów × cena) per typ
        int TotalWinningBets,        // Ilość wygrywających zakładów
        decimal TotalWinPrize,       // Suma wygranych
        decimal Balance,             // Bilans = TotalWinPrize - TotalCost
        List<WinTierSummaryDto> WinsByTier
    );

    public record WinTierSummaryDto(
        int WinTier,
        int WinCount,
        decimal WinPrize
    );
}