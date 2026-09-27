using App01.Shared.Application.Exceptions;
using App01.Shared.Application.Interfaces;
using App01.Shared.Infrastructure.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;


namespace App01.Modules.Lotto.Features.DrawsImport;


public class DrawsImportHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<DrawsImportHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public DrawsImportHandler(
        ILogger<DrawsImportHandler> logger,
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

        var isCallerAdmin = await _jwtService.GetIsAdminFromJwt();
        if (!isCallerAdmin)
        {
            throw new ForbiddenException("Only administrators can import draws");
        }

        var draws = await ParseInputAsync(request, cancellationToken);

        return await SaveToCsvForWorker(draws, cancellationToken);
    }

    private async Task<List<Contracts.DrawDto>> ParseInputAsync(Contracts.Request request, CancellationToken cancellationToken)
    {
        if (request.Draws != null && request.Draws.Count > 0)
        {
            return request.Draws;
        }

        if (string.IsNullOrWhiteSpace(request.Csv))
        {
            throw new ValidationException("No data provided");
        }

        return await ParseCsvAsync(request.Csv, request.DrawTypeId!.Value, cancellationToken);
    }

    private async Task<List<Contracts.DrawDto>> ParseCsvAsync(string csv, int drawTypeId, CancellationToken cancellationToken)
    {
        var result = new List<Contracts.DrawDto>();
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

            var draw = ParseCsvLine(trimmedLine, separator, drawTypeId, specialsCount);
            if (draw != null)
            {
                result.Add(draw);
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
        return lowerLine.Contains("drawsystemid") ||
               lowerLine.Contains("drawdate") ||
               lowerLine.Contains("number1") ||
               lowerLine.Contains("nr los") ||
               lowerLine.Contains("data");
    }

    private Contracts.DrawDto? ParseCsvLine(string line, char separator, int drawTypeId, int specialsCount)
    {
        string[] parts = separator == ' '
            ? line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            : line.Split(separator);

        if (parts.Length < 3)
        {
            _logger.LogWarning("CSV line has too few columns: {Line}", line);
            return null;
        }

        if (!long.TryParse(parts[0].Trim(), out var drawSystemId))
        {
            _logger.LogWarning("Cannot parse DrawSystemId from: {Value}", parts[0]);
            return null;
        }

        if (!TryParseDate(parts[1].Trim(), out var drawDate))
        {
            _logger.LogWarning("Cannot parse DrawDate from: {Value}", parts[1]);
            return null;
        }

        var numbers = new List<int>();
        var specials = new List<int>();

        for (int i = 2; i < parts.Length; i++)
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

        // Kaskada (DrawTypeId = 8) - copy all numbers to specials
        if (drawTypeId == 8)
        {
            specials.AddRange(numbers);
        }
        // Move last N numbers to specials based on DrawType.SpecialsCount
        else if (specialsCount > 0 && specials.Count == 0 && numbers.Count > specialsCount)
        {
            for (int i = 0; i < specialsCount; i++)
            {
                var specialValue = numbers[numbers.Count - specialsCount + i];
                // MultiMulti (DrawTypeId = 9): value 0 means "no Plus" - skip adding to specials
                if (drawTypeId == 9 && specialValue == 0)
                    continue;
                specials.Add(specialValue);
            }
            numbers.RemoveRange(numbers.Count - specialsCount, specialsCount);
        }

        return new Contracts.DrawDto(
            DrawSystemId: drawSystemId,
            DrawDate: drawDate,
            DrawTypeId: drawTypeId,
            Numbers: numbers,
            Specials: specials
        );
    }

    private static bool TryParseDate(string value, out DateTime result)
    {
        var utcZFormats = new[] { "yyyy-MM-ddTHH:mm:ssZ", "yyyy-MM-ddTHH:mm:ss'Z'" };

        foreach (var format in utcZFormats)
        {
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out result))
            {
                result = DateTime.SpecifyKind(result, DateTimeKind.Utc);
                return true;
            }
        }

        if (value.Contains('+') || (value.Contains('-') && value.LastIndexOf('-') > 7))
        {
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            {
                result = dto.UtcDateTime;
                return true;
            }
        }

        var localWithTimeFormats = new[]
        {
            "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm",
            "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm"
        };

        foreach (var format in localWithTimeFormats)
        {
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var localDate))
            {
                result = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate, DateTimeKind.Local));
                return true;
            }
        }

        var dateOnlyFormats = new[] { "yyyy-MM-dd", "dd.MM.yyyy", "dd-MM-yyyy", "MM/dd/yyyy", "dd/MM/yyyy" };

        foreach (var format in dateOnlyFormats)
        {
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
            {
                var localMidnight = DateTime.SpecifyKind(dateOnly, DateTimeKind.Local);
                result = TimeZoneInfo.ConvertTimeToUtc(localMidnight);
                return true;
            }
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            if (parsed.Kind == DateTimeKind.Utc)
            {
                result = parsed;
            }
            else if (parsed.TimeOfDay == TimeSpan.Zero)
            {
                var localMidnight = DateTime.SpecifyKind(parsed, DateTimeKind.Local);
                result = TimeZoneInfo.ConvertTimeToUtc(localMidnight);
            }
            else
            {
                result = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(parsed, DateTimeKind.Local));
            }
            return true;
        }

        result = default;
        return false;
    }

    private async Task<Contracts.Response> SaveToCsvForWorker(List<Contracts.DrawDto> draws, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var savedCount = 0;

        var drawTypeIds = draws.Select(d => d.DrawTypeId).Distinct().ToList();

        var drawTypes = await _dbContext.DrawTypes
            .AsNoTracking()
            .Where(dt => drawTypeIds.Contains(dt.Id))
            .ToDictionaryAsync(dt => dt.Id, dt => dt.Name, cancellationToken);

        var drawsByType = draws
            .GroupBy(d => d.DrawTypeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TMP", "LOTTO", "CSV");
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        foreach (var (drawTypeId, drawList) in drawsByType)
        {
            if (!drawTypes.TryGetValue(drawTypeId, out var drawTypeName))
            {
                errors.Add($"Unknown DrawTypeId: {drawTypeId}");
                continue;
            }

            try
            {
                var fileDateTime = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                var fileName = $"Data_{drawTypeId:D2}_{drawTypeName}_{fileDateTime}.csv";
                var filePath = Path.Combine(directoryPath, fileName);

                var csvLines = new List<string>();

                foreach (var drawDto in drawList)
                {
                    var specials = drawTypeId == 8
                        ? drawDto.Numbers
                        : (drawDto.Specials ?? []);

                    var csvParts = new List<string>
                    {
                        drawDto.DrawSystemId.ToString("D20"),
                        drawDto.DrawDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
                    };

                    csvParts.AddRange(drawDto.Numbers.Select(n => n.ToString()));
                    csvParts.AddRange(specials.Select(s => s.ToString()));

                    csvLines.Add(string.Join(",", csvParts));
                    savedCount++;
                }

                await File.WriteAllLinesAsync(filePath, csvLines, cancellationToken);
                _logger.LogInformation("SaveToCsvForWorker: Created CSV file: {FileName} with {Count} lines", fileName, csvLines.Count);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to save CSV for DrawTypeId {drawTypeId}: {ex.Message}");
            }
        }

        return new Contracts.Response(savedCount, errors);
    }
}
