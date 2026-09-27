using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Lotto.Features.DrawsNumbersStatsList;


public class Handler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<Handler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;


    public Handler(
        ILogger<Handler> logger,
        IValidator<Contracts.Request> validator,
        AppDbContext dbContext)
    {
        _logger = logger;
        _validator = validator;
        _dbContext = dbContext;
    }

    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var drawType = await _dbContext.DrawTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(dt => dt.Id == request.DrawTypeId, cancellationToken);

        if (drawType == null)
        {
            throw new ValidationException($"DrawType with Id {request.DrawTypeId} not found");
        }

        if (request.NumbersGroup > drawType.NumbersCount)
        {
            throw new ValidationException($"NumbersGroup ({request.NumbersGroup}) cannot be greater than NumbersCount ({drawType.NumbersCount}) for this DrawType");
        }

        var query = _dbContext.Draws
            .AsNoTracking()
            .Where(d => d.DrawTypeId == request.DrawTypeId);

        if (request.DrawDateFrom.HasValue)
        {
            query = query.Where(d => d.DrawDate >= request.DrawDateFrom.Value);
        }

        if (request.DrawDateTo.HasValue)
        {
            query = query.Where(d => d.DrawDate <= request.DrawDateTo.Value);
        }

        var draws = await query.ToListAsync(cancellationToken);
        var totalDrawsCount = draws.Count;

        var sortAscending = request.SortOrder?.ToLowerInvariant() == "asc";
        _logger.LogInformation("SortOrder received: '{SortOrder}', sortAscending: {SortAscending}", request.SortOrder, sortAscending);

        // Calculate stats for Numbers
        var numbersStats = CalculateGroupStats(draws.Select(d => d.Numbers), request.NumbersGroup, sortAscending);

        // Calculate stats for Specials (only if drawType has specials and group size is specified)
        var specialsStats = new List<Contracts.NumbersGroupStatsDto>();
        if (drawType.SpecialsCount > 0 && request.SpecialsGroup.HasValue && request.SpecialsGroup.Value >= 1 && request.SpecialsGroup.Value <= drawType.SpecialsCount)
        {
            specialsStats = CalculateGroupStats(draws.Select(d => d.Specials), request.SpecialsGroup.Value, sortAscending);
        }

        _logger.LogDebug("Calculated stats for {NumbersGroupsCount} number groups and {SpecialsGroupsCount} specials groups from {DrawsCount} draws",
            numbersStats.Count, specialsStats.Count, totalDrawsCount);

        return new Contracts.Response(numbersStats, specialsStats, totalDrawsCount, numbersStats.Count, specialsStats.Count);
    }

    private static List<Contracts.NumbersGroupStatsDto> CalculateGroupStats(IEnumerable<List<int>> numbersLists, int groupSize, bool sortAscending)
    {
        var groupCounts = new Dictionary<string, (List<int> Numbers, int Count)>();

        foreach (var numbers in numbersLists)
        {
            if (numbers == null || numbers.Count < groupSize)
                continue;

            var sortedNumbers = numbers.OrderBy(n => n).ToList();
            var combinations = GetCombinations(sortedNumbers, groupSize);

            foreach (var combination in combinations)
            {
                var key = string.Join(",", combination);
                if (groupCounts.TryGetValue(key, out var existing))
                {
                    groupCounts[key] = (existing.Numbers, existing.Count + 1);
                }
                else
                {
                    groupCounts[key] = (combination, 1);
                }
            }
        }

        var ordered = sortAscending
            ? groupCounts.Values.OrderBy(x => x.Count).ThenBy(x => x.Numbers[0])
            : groupCounts.Values.OrderByDescending(x => x.Count).ThenBy(x => x.Numbers[0]);

        return ordered
            .Select(x => new Contracts.NumbersGroupStatsDto(x.Numbers, x.Count))
            .ToList();
    }

    private static List<List<int>> GetCombinations(List<int> numbers, int groupSize)
    {
        var result = new List<List<int>>();
        GenerateCombinations(numbers, groupSize, 0, new List<int>(), result);
        return result;
    }

    private static void GenerateCombinations(List<int> numbers, int groupSize, int start, List<int> current, List<List<int>> result)
    {
        if (current.Count == groupSize)
        {
            result.Add(new List<int>(current));
            return;
        }

        for (int i = start; i < numbers.Count; i++)
        {
            current.Add(numbers[i]);
            GenerateCombinations(numbers, groupSize, i + 1, current, result);
            current.RemoveAt(current.Count - 1);
        }
    }
}