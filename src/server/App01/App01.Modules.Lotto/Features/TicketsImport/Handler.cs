using App01.Shared.Application.Entities.Lotto;
using App01.Shared.Application.Interfaces;
using App01.Shared.Infrastructure.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Lotto.Features.TicketsImport;


public class TicketsImportHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<TicketsImportHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;


    public TicketsImportHandler(
        ILogger<TicketsImportHandler> logger,
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

        var currentUserId = await _jwtService.GetUserIdFromJwt();

        var tickets = await ParseInputAsync(request, cancellationToken);

        return await SaveToDatabase(tickets, currentUserId, cancellationToken);
    }

    private async Task<List<Contracts.TicketDto>> ParseInputAsync(Contracts.Request request, CancellationToken cancellationToken)
    {
        if (request.Tickets != null && request.Tickets.Count > 0)
        {
            return request.Tickets;
        }

        if (string.IsNullOrWhiteSpace(request.Csv))
        {
            throw new ValidationException("No data provided");
        }

        return await ParseCsvAsync(request.Csv, request.DrawTypeId!.Value, request.GroupName, cancellationToken);
    }

    private async Task<List<Contracts.TicketDto>> ParseCsvAsync(string csv, int drawTypeId, string? groupName, CancellationToken cancellationToken)
    {
        var result = new List<Contracts.TicketDto>();
        var lines = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
        {
            throw new ValidationException("CSV is empty");
        }

        var drawType = await _dbContext.DrawTypes
            .FirstOrDefaultAsync(dt => dt.Id == drawTypeId, cancellationToken);

        var specialsCount = drawType?.SpecialsCount ?? 0;
        var separator = DetectSeparator(lines[0]);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            if (IsHeaderLine(trimmedLine))
                continue;

            var ticket = ParseCsvLine(trimmedLine, separator, drawTypeId, groupName, specialsCount);
            if (ticket != null)
            {
                result.Add(ticket);
            }
        }

        return result;
    }

    private static char DetectSeparator(string line)
    {
        var semicolonCount = line.Count(c => c == ';');
        var commaCount = line.Count(c => c == ',');
        var tabCount = line.Count(c => c == '\t');

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var spaceCount = parts.Length > 1 ? parts.Length - 1 : 0;

        var maxCount = new[] { semicolonCount, commaCount, tabCount, spaceCount }.Max();

        if (maxCount == 0)
            return ',';

        if (semicolonCount == maxCount) return ';';
        if (commaCount == maxCount) return ',';
        if (tabCount == maxCount) return '\t';

        return ' ';
    }

    private static bool IsHeaderLine(string line)
    {
        var lowerLine = line.ToLowerInvariant();
        return lowerLine.Contains("drawtypeid") ||
               lowerLine.Contains("groupname") ||
               lowerLine.Contains("number") ||
               lowerLine.Contains("special");
    }

    private Contracts.TicketDto? ParseCsvLine(string line, char separator, int drawTypeId, string? groupName, int specialsCount)
    {
        string[] parts = separator == ' '
            ? line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            : line.Split(separator);

        if (parts.Length < 1)
        {
            _logger.LogWarning("CSV line has too few columns: {Line}", line);
            return null;
        }

        var numbers = new List<int>();
        var specials = new List<int>();
        var parsedDrawTypeId = drawTypeId;
        // Request groupName always takes precedence if provided
        var parsedGroupName = groupName;
        var startIndex = 0;

        // Try to parse DrawTypeId from first column if it's a number
        if (int.TryParse(parts[0].Trim(), out var firstCol))
        {
            // Check if it looks like a DrawTypeId (1-10) or a lottery number (higher)
            if (firstCol >= 1 && firstCol <= 10 && parts.Length > 1)
            {
                parsedDrawTypeId = firstCol;
                startIndex = 1;

                // Check if second column is GroupName (non-numeric or empty)
                if (parts.Length > 2 && !int.TryParse(parts[1].Trim(), out _))
                {
                    // Only use CSV groupName if request groupName is not provided
                    if (string.IsNullOrWhiteSpace(groupName) && !string.IsNullOrWhiteSpace(parts[1].Trim()))
                    {
                        parsedGroupName = parts[1].Trim();
                    }
                    startIndex = 2;
                }
            }
        }
        else
        {
            // First column is not a number, might be GroupName
            // Only use CSV groupName if request groupName is not provided
            if (string.IsNullOrWhiteSpace(groupName) && !string.IsNullOrWhiteSpace(parts[0].Trim()))
            {
                parsedGroupName = parts[0].Trim();
            }
            startIndex = 1;
        }

        for (int i = startIndex; i < parts.Length; i++)
        {
            var value = parts[i].Trim();
            if (string.IsNullOrEmpty(value))
                continue;

            if (value.StartsWith("S:", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("SPEC:", StringComparison.OrdinalIgnoreCase))
            {
                var specialValue = value.Contains(':') ? value.Split(':')[1] : value;
                if (int.TryParse(specialValue, out var specialNum))
                {
                    specials.Add(specialNum);
                }
            }
            else if (int.TryParse(value, out var num))
            {
                numbers.Add(num);
            }
        }

        if (numbers.Count == 0)
        {
            _logger.LogWarning("No numbers parsed from line: {Line}", line);
            return null;
        }

        // Move last N numbers to specials based on DrawType.SpecialsCount
        if (specialsCount > 0 && specials.Count == 0 && numbers.Count > specialsCount)
        {
            for (int i = 0; i < specialsCount; i++)
            {
                specials.Add(numbers[numbers.Count - specialsCount + i]);
            }
            numbers.RemoveRange(numbers.Count - specialsCount, specialsCount);
        }

        return new Contracts.TicketDto(
            DrawTypeId: parsedDrawTypeId,
            GroupName: parsedGroupName,
            Numbers: numbers,
            Specials: specials
        );
    }

    private async Task<Contracts.Response> SaveToDatabase(List<Contracts.TicketDto> tickets, long userId, CancellationToken cancellationToken)
    {
        var importedCount = 0;
        var errors = new List<string>();

        for (var i = 0; i < tickets.Count; i++)
        {
            var ticketDto = tickets[i];
            try
            {
                var ticket = new Ticket
                {
                    Id = 0,
                    UserId = userId,
                    DrawTypeId = ticketDto.DrawTypeId,
                    GroupName = ticketDto.GroupName,
                    CreatedAt = DateTime.UtcNow,
                    Numbers = [.. ticketDto.Numbers.OrderBy(n => n)],
                    Specials = [.. ticketDto.Specials.OrderBy(n => n)],
                };

                _dbContext.Tickets.Add(ticket);
                importedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to import ticket at index {i}: {ex.Message}");
            }
        }

        if (importedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Imported {ImportedCount} tickets for user {UserId}", importedCount, userId);

        return new Contracts.Response(importedCount, 0, errors);
    }
}
