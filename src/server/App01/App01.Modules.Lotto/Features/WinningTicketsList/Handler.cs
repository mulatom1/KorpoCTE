using App01.Shared.Application.Entities.Lotto;
using App01.Shared.Application.Interfaces;
using App01.Shared.Infrastructure.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Lotto.Features.WinningTicketsList;


public class WinningTicketsListHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<WinningTicketsListHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;


    public WinningTicketsListHandler(
        ILogger<WinningTicketsListHandler> logger,
        IValidator<Contracts.Request> validator,
        AppDbContext dbContext,
        IJwtService jwtService)
    {
        _logger = logger;
        _validator = validator;
        _dbContext = dbContext;
        _jwtService = jwtService;
    }

    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var userId = await _jwtService.GetUserIdFromJwt();
        var enabledWinTiers = request.GetEnabledWinTiers();

        // 1. Pobierz reguły wygranych i ceny kuponów
        var winTierRules = await _dbContext.DrawTypeWinTiers.ToListAsync(cancellationToken);
        var drawTypePrices = await _dbContext.DrawTypes
            .ToDictionaryAsync(dt => dt.Id, dt => dt.TicketPrize, cancellationToken);

        // 2. Pobierz przefiltrowane LOSOWANIA
        var drawsQuery = _dbContext.Draws.AsQueryable();
        if (request.DrawDateFrom.HasValue)
            drawsQuery = drawsQuery.Where(d => d.DrawDate >= request.DrawDateFrom.Value);
        if (request.DrawDateTo.HasValue)
            drawsQuery = drawsQuery.Where(d => d.DrawDate <= request.DrawDateTo.Value);
        if (request.DrawTypeId.HasValue)
            drawsQuery = drawsQuery.Where(d => d.DrawTypeId == request.DrawTypeId.Value);

        var draws = await drawsQuery.OrderByDescending(d => d.DrawDate).ToListAsync(cancellationToken);

        // 3. Pobierz przefiltrowane KUPONY użytkownika
        var ticketsQuery = _dbContext.Tickets.Where(t => t.UserId == userId);
        if (request.DrawTypeId.HasValue)
            ticketsQuery = ticketsQuery.Where(t => t.DrawTypeId == request.DrawTypeId.Value);
        if (!string.IsNullOrWhiteSpace(request.GroupName))
            ticketsQuery = ticketsQuery.Where(t => t.GroupName != null && t.GroupName.Contains(request.GroupName));

        var tickets = await ticketsQuery.ToListAsync(cancellationToken);
        var ticketsByDrawType = tickets.GroupBy(t => t.DrawTypeId).ToDictionary(g => g.Key, g => g.ToList());

        // 4. Buduj dopasowania (dla każdego losowania znajdź wygrywające kupony)
        var allWinningMatches = new List<(Draw Draw, Contracts.MatchingTicketDto Ticket)>();
        var winTierCounts = new Dictionary<int, int>();
        var winTierPrizes = new Dictionary<int, decimal>();

        foreach (var draw in draws)
        {
            var drawNumbers = draw.Numbers.ToHashSet();
            var drawSpecials = draw.Specials.ToHashSet();

            if (!ticketsByDrawType.TryGetValue(draw.DrawTypeId, out var ticketsForType))
                continue;

            foreach (var ticket in ticketsForType)
            {
                // Porównaj numery
                var matchedNumbers = ticket.Numbers.Where(n => drawNumbers.Contains(n)).ToList();

                // Dla MultiMulti (typ 9): Plus = czy którakolwiek z Numbers trafiła w Specials losowania
                List<int> matchedSpecials;
                int specialsMatchCount;
                int specialsSelected;

                if (draw.DrawTypeId == 9)
                {
                    matchedSpecials = ticket.Numbers.Where(n => drawSpecials.Contains(n)).ToList();
                    specialsMatchCount = matchedSpecials.Count > 0 ? 1 : 0;
                    specialsSelected = specialsMatchCount;
                }
                else
                {
                    matchedSpecials = ticket.Specials.Where(n => drawSpecials.Contains(n)).ToList();
                    specialsMatchCount = matchedSpecials.Count;
                    specialsSelected = ticket.Specials.Count;
                }

                // Sprawdź czy jest jakiekolwiek trafienie
                if (matchedNumbers.Count == 0 && matchedSpecials.Count == 0)
                    continue;

                // Znajdź stopień wygranej
                var (winTier, winPrize) = GetWinTierAndPrize(
                    winTierRules,
                    draw.DrawTypeId,
                    matchedNumbers.Count,
                    ticket.Numbers.Count,
                    specialsMatchCount,
                    specialsSelected);

                if (winTier == 0)
                    continue;

                // Filtruj po włączonych stopniach wygranej
                if (!enabledWinTiers.Contains(winTier))
                    continue;

                var ticketPrice = drawTypePrices.GetValueOrDefault(ticket.DrawTypeId, 0m);
                var matchingTicket = new Contracts.MatchingTicketDto(
                    ticket.Id,
                    ticket.DrawTypeId,
                    ticket.GroupName,
                    ticket.CreatedAt,
                    ticket.Numbers,
                    ticket.Specials,
                    matchedNumbers,
                    matchedSpecials,
                    winTier,
                    winPrize,
                    ticketPrice
                );

                allWinningMatches.Add((draw, matchingTicket));

                // Aktualizuj liczniki wygranych per tier
                if (!winTierCounts.ContainsKey(winTier))
                {
                    winTierCounts[winTier] = 0;
                    winTierPrizes[winTier] = 0m;
                }
                winTierCounts[winTier]++;
                winTierPrizes[winTier] += winPrize;
            }
        }

        // 5. Grupuj wygrywające kupony po DrawId
        var winningMatchesByDraw = allWinningMatches
            .GroupBy(m => m.Draw.Id)
            .ToDictionary(g => g.Key, g => g.Select(m => m.Ticket).ToList());

        // 6. Filtruj losowania bez dopasowań jeśli ustawiono flagę
        var filteredDraws = request.HideDrawsWithoutMatches
            ? draws.Where(d => winningMatchesByDraw.ContainsKey(d.Id)).ToList()
            : draws;

        // 7. Paginacja na poziomie losowań
        var totalCount = filteredDraws.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);
        var offset = (request.Page - 1) * request.PageSize;

        var paginatedDraws = filteredDraws
            .Skip(offset)
            .Take(request.PageSize)
            .ToList();

        // 8. Twórz DTO dla losowań
        var drawDtos = paginatedDraws
            .Select(draw => new Contracts.DrawWithTicketsDto(
                draw.Id,
                draw.DrawSystemId,
                DateTime.SpecifyKind(draw.DrawDate, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                draw.DrawTypeId,
                drawTypePrices.GetValueOrDefault(draw.DrawTypeId, 0m),
                draw.Numbers,
                draw.Specials,
                winningMatchesByDraw.GetValueOrDefault(draw.Id, new List<Contracts.MatchingTicketDto>())
            ))
            .ToList();

        // 9. PODSUMOWANIE
        // Grupuj kupony i losowania per typ gry (do obliczenia kosztów)
        var ticketCountsByType = tickets
            .GroupBy(t => t.DrawTypeId)
            .ToDictionary(g => g.Key, g => g.Count());

        var drawCountsByType = draws
            .GroupBy(d => d.DrawTypeId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Ilość losowań (przefiltrowanych)
        var totalDraws = draws.Count;

        // Ilość kuponów (przefiltrowanych)
        var totalTickets = tickets.Count;

        // Suma zakładów = Σ(ilość losowań danego typu × ilość kuponów danego typu)
        var totalBets = ticketCountsByType.Sum(tc =>
            tc.Value * drawCountsByType.GetValueOrDefault(tc.Key, 0));

        // Koszty kuponów = Σ(ilość losowań × ilość kuponów × cena) per typ
        var totalCost = ticketCountsByType.Sum(tc =>
            tc.Value * drawCountsByType.GetValueOrDefault(tc.Key, 0) * drawTypePrices.GetValueOrDefault(tc.Key, 0m));

        // Wygrane zakłady i suma wygranych
        var totalWinningBets = winTierCounts.Values.Sum();
        var totalWinPrize = winTierPrizes.Values.Sum();

        // Bilans = Suma wygranych - Koszty kuponów
        var balance = totalWinPrize - totalCost;

        var winsByTier = winTierCounts.Keys
            .OrderBy(tier => tier)
            .Select(tier => new Contracts.WinTierSummaryDto(tier, winTierCounts[tier], winTierPrizes[tier]))
            .ToList();

        var summary = new Contracts.SummaryDto(
            TotalDraws: totalDraws,
            TotalTickets: totalTickets,
            TotalBets: totalBets,
            TotalCost: totalCost,
            TotalWinningBets: totalWinningBets,
            TotalWinPrize: totalWinPrize,
            Balance: balance,
            WinsByTier: winsByTier
        );

        _logger.LogDebug("Retrieved {DrawCount} draws for user {UserId} (page {Page}/{TotalPages}), {WinningBets} winning bets",
            drawDtos.Count, userId, request.Page, totalPages, totalWinningBets);

        return new Contracts.Response(drawDtos, summary, totalCount, request.Page, request.PageSize, totalPages);
    }

    private static (int WinTier, decimal WinPrize) GetWinTierAndPrize(
        List<DrawTypeWinTier> rules,
        int drawTypeId,
        int numbersMatchCount,
        int numbersSelected,
        int specialsMatchCount,
        int specialsSelected)
    {
        var matchingRule = rules
            .Where(r => r.DrawTypeId == drawTypeId
                && r.NumbersMatchCount == numbersMatchCount
                && r.NumbersMatchSelected == numbersSelected
                && r.SpecialsMatchCount == specialsMatchCount
                && r.SpecialsMatchSelected == specialsSelected)
            .OrderByDescending(r => r.WinTier)
            .FirstOrDefault();

        if (matchingRule == null)
            return (0, 0m);

        return (matchingRule.WinTier, matchingRule.PotentialWinPrize);
    }
}
