using System.Globalization;
using System.Text.RegularExpressions;

using App01.Shared.Application.Entities.Lotto;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Lotto.Workers;

public class LottoWorker03(
    ILogger<LottoWorker03> logger,
    IConfiguration configuration,
    IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private readonly ILogger<LottoWorker03> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("{ClassName}: Started.", GetType().Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = _configuration.GetValue("Workers:LottoWorker03:IntervalMinutes", 1.0);
            if (!_configuration.GetValue("Workers:LottoWorker03:Enabled", true))
            {
                _logger.LogDebug("{ClassName}: Service disabled.", GetType().Name);
                await Task.Delay((int)(interval * 1000 * 60), stoppingToken);
                continue;
            }

            var fileInfo = GetFileToProcess();
            if (fileInfo == null)
            {
                var freezingMinutes = _configuration.GetValue("Workers:LottoWorker03:FreezingMinutes", 30.0);
                _logger.LogDebug("{ClassName}: No file to process. Freezing for {Minutes} minutes.", GetType().Name, freezingMinutes);
                await Task.Delay(TimeSpan.FromMinutes(freezingMinutes), stoppingToken);
                continue;
            }

            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<AppDbContext>()
                ?? throw new InvalidOperationException("AppDbContext not available.");

            await ProcessCsvFile(fileInfo.Value.FilePath, fileInfo.Value.DrawTypeId, dbContext, stoppingToken);

            _logger.LogDebug("{ClassName}: Freezed.", GetType().Name);
            await Task.Delay((int)(interval * 1000 * 60), stoppingToken);
        }
    }

    private (string FilePath, int DrawTypeId)? GetFileToProcess()
    {
        _logger.LogInformation("{ClassName}: Get file to process - start", GetType().Name);

        try
        {
            var directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TMP", "LOTTO", "CSV");
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                return null;
            }

            var filesErr = Directory.GetFiles(directoryPath, "*.err").Select(Path.GetFileNameWithoutExtension).ToHashSet();
            var filesCsv = Directory.GetFiles(directoryPath, "*.csv");

            // Filtruj pliki CSV, które nie mają odpowiednika .err (sukces = CSV usunięte, błąd = CSV + .err)
            var unprocessedFiles = filesCsv.Where(csvFile =>
            {
                var baseName = Path.GetFileNameWithoutExtension(csvFile);
                return !filesErr.Contains(baseName);
            }).ToArray();

            if (unprocessedFiles.Length == 0)
                return null;

            var filePath = unprocessedFiles[0];
            var drawTypeId = ExtractDrawTypeId(filePath);

            if (drawTypeId == null)
            {
                _logger.LogWarning("{ClassName}: Cannot extract DrawTypeId from file name: {FileName}", GetType().Name, Path.GetFileName(filePath));
                return null;
            }

            return (filePath, drawTypeId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName}: File to process Exception: {Message}", GetType().Name, ex.Message);
            return null;
        }
    }

    private static int? ExtractDrawTypeId(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Pattern: Data_XX_*.csv where XX is DrawTypeId (01, 02, etc.)
        // Examples:
        // "Data_01_2024" -> 1 (Lotto)
        // "Data_02_2024" -> 2 (LottoPlus)
        // "Data_06_EuroJackpot" -> 6 (EuroJackpot)
        var match = Regex.Match(fileName, @"^Data_(\d{2})_", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var drawTypeId))
        {
            return drawTypeId;
        }

        return null;
    }

    private async Task ProcessCsvFile(string filePath, int drawTypeId, AppDbContext dbContext, CancellationToken stoppingToken)
    {
        _logger.LogDebug("{ClassName}: Processing CSV file started: {FilePath}", GetType().Name, filePath);

        try
        {
            var content = await File.ReadAllTextAsync(filePath, stoppingToken);
            var draws = await ParseCsvAsync(content, drawTypeId, dbContext, stoppingToken);

            if (draws.Count == 0)
            {
                _logger.LogDebug("{ClassName}: No draws found in file {FileName}. Deleting CSV.", GetType().Name, Path.GetFileName(filePath));
                DeleteCsvFile(filePath);
                return;
            }

            var (imported, updated, skipped) = await SaveDraws(draws, drawTypeId, dbContext, stoppingToken);

            var statusMessage = $"Imported: {imported}, Updated: {updated}, Skipped: {skipped}";
            _logger.LogDebug("{ClassName}: Processing end. {Status}. Deleting CSV.", GetType().Name, statusMessage);
            DeleteCsvFile(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName}: Processing: Failed processing file {FileName}: {Message}", GetType().Name, Path.GetFileName(filePath), ex.Message);
            await CreateErrorFile(filePath, $"Error: {ex.Message}\n{ex.InnerException?.Message}\nTimestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC", stoppingToken);
        }
    }

    private void DeleteCsvFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName}: Failed to delete CSV file {FileName}", GetType().Name, Path.GetFileName(filePath));
        }
    }

    private async Task CreateErrorFile(string filePath, string content, CancellationToken stoppingToken)
    {
        try
        {
            var errorFilePath = Path.ChangeExtension(filePath, ".err");
            await File.WriteAllTextAsync(errorFilePath, content, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName}: Failed to create error file for {FileName}", GetType().Name, Path.GetFileName(filePath));
        }
    }

    private async Task<List<DrawDto>> ParseCsvAsync(string csv, int drawTypeId, AppDbContext dbContext, CancellationToken stoppingToken)
    {
        var result = new List<DrawDto>();
        var lines = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
        {
            return result;
        }

        // Get DrawType to know how many specials to expect
        var drawType = await dbContext.DrawTypes
            .FirstOrDefaultAsync(dt => dt.Id == drawTypeId, stoppingToken);

        var specialsCount = drawType?.SpecialsCount ?? 0;

        // Detect separator from first line
        var separator = DetectSeparator(lines[0]);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            // Skip header line if detected
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
        // Count occurrences of each potential separator
        var semicolonCount = line.Count(c => c == ';');
        var commaCount = line.Count(c => c == ',');
        var tabCount = line.Count(c => c == '\t');

        // Space is tricky - count sequences of spaces as single separator
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var spaceCount = parts.Length > 1 ? parts.Length - 1 : 0;

        // Choose the most common separator
        var maxCount = new[] { semicolonCount, commaCount, tabCount, spaceCount }.Max();

        if (maxCount == 0)
            return ','; // Default to comma

        if (semicolonCount == maxCount) return ';';
        if (commaCount == maxCount) return ',';
        if (tabCount == maxCount) return '\t';

        return ' '; // Space
    }

    private static bool IsHeaderLine(string line)
    {
        var lowerLine = line.ToLowerInvariant();
        return lowerLine.Contains("drawsystemid") ||
               lowerLine.Contains("drawdate") ||
               lowerLine.Contains("number1");
    }

    private DrawDto? ParseCsvLine(string line, char separator, int drawTypeId, int specialsCount)
    {
        string[] parts;

        if (separator == ' ')
        {
            parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }
        else
        {
            parts = line.Split(separator);
        }

        if (parts.Length < 3)
        {
            _logger.LogWarning("{ClassName}: CSV line has too few columns: {Line}", GetType().Name, line);
            return null;
        }

        // First column: DrawSystemId
        if (!long.TryParse(parts[0].Trim(), out var drawSystemId))
        {
            _logger.LogWarning("{ClassName}: Cannot parse DrawSystemId from: {Value}", GetType().Name, parts[0]);
            return null;
        }

        // Second column: DrawDate
        if (!TryParseDate(parts[1].Trim(), out var drawDate))
        {
            _logger.LogWarning("{ClassName}: Cannot parse DrawDate from: {Value}", GetType().Name, parts[1]);
            return null;
        }

        // Remaining columns: Numbers
        var numbers = new List<int>();
        var specials = new List<int>();

        for (int i = 2; i < parts.Length; i++)
        {
            var value = parts[i].Trim();
            if (string.IsNullOrEmpty(value))
                continue;

            // Check if this might be a special number marker (e.g., "S:5" or starts with special prefix)
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
            _logger.LogWarning("{ClassName}: No numbers parsed from line: {Line}", GetType().Name, line);
            return null;
        }

        // Kaskada (DrawTypeId = 8) - copy all numbers to specials (same numbers in both)
        if (drawTypeId == 8)
        {
            specials.AddRange(numbers);
        }
        // Move last N numbers to specials based on DrawType.SpecialsCount (specials are at the end of CSV)
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

        return new DrawDto(drawSystemId, drawDate, [.. numbers], [.. specials]);
    }

    private static bool TryParseDate(string value, out DateTime result)
    {
        // 1. Formaty UTC z "Z" na końcu
        var utcZFormats = new[]
        {
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss'Z'",
        };

        foreach (var format in utcZFormats)
        {
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out result))
            {
                result = DateTime.SpecifyKind(result, DateTimeKind.Utc);
                return true;
            }
        }

        // 2. Formaty z offset timezone (+01:00, -05:00, itp.) - parsuj do UTC przez DateTimeOffset
        if (value.Contains('+') || (value.Contains('-') && value.LastIndexOf('-') > 7))
        {
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            {
                result = dto.UtcDateTime;
                return true;
            }
        }

        // 3. Formaty lokalne Z CZASEM (bez timezone) - konwertuj lokalny do UTC
        var localWithTimeFormats = new[]
        {
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "dd.MM.yyyy HH:mm:ss",
            "dd.MM.yyyy HH:mm",
        };

        foreach (var format in localWithTimeFormats)
        {
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var localDate))
            {
                // Czas podany - konwertuj z lokalnego na UTC
                result = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate, DateTimeKind.Local));
                return true;
            }
        }

        // 4. Formaty TYLKO DATA (bez czasu) - traktuj jako 00:00 czasu lokalnego i konwertuj na UTC
        var dateOnlyFormats = new[]
        {
            "yyyy-MM-dd",
            "dd.MM.yyyy",
            "dd-MM-yyyy",
            "MM/dd/yyyy",
            "dd/MM/yyyy"
        };

        foreach (var format in dateOnlyFormats)
        {
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
            {
                // Traktuj jako 00:00 czasu lokalnego i konwertuj na UTC
                var localMidnight = DateTime.SpecifyKind(dateOnly, DateTimeKind.Local);
                result = TimeZoneInfo.ConvertTimeToUtc(localMidnight);
                return true;
            }
        }

        // 5. Fallback - standardowe parsowanie
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            if (parsed.Kind == DateTimeKind.Utc)
            {
                result = parsed;
            }
            else if (parsed.TimeOfDay == TimeSpan.Zero)
            {
                // Tylko data - traktuj jako 00:00 czasu lokalnego i konwertuj na UTC
                var localMidnight = DateTime.SpecifyKind(parsed, DateTimeKind.Local);
                result = TimeZoneInfo.ConvertTimeToUtc(localMidnight);
            }
            else
            {
                // Czas lokalny - konwertuj na UTC
                result = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(parsed, DateTimeKind.Local));
            }
            return true;
        }

        result = default;
        return false;
    }

    private async Task<(int Imported, int Updated, int Skipped)> SaveDraws(List<DrawDto> draws, int drawTypeId, AppDbContext dbContext, CancellationToken stoppingToken)
    {
        _logger.LogDebug("{ClassName}: SaveDraws started for DrawTypeId: {DrawTypeId}", GetType().Name, drawTypeId);

        var imported = 0;
        var updated = 0;
        var skipped = 0;
        var dbActionsCount = 0;

        var drawSystemIds = draws.Select(d => d.DrawSystemId).ToList();
        var existingDraws = await dbContext.Draws
            .Where(d => d.DrawTypeId == drawTypeId && drawSystemIds.Contains(d.DrawSystemId))
            .ToDictionaryAsync(d => d.DrawSystemId, stoppingToken);

        foreach (var drawDto in draws)
        {
            if (existingDraws.TryGetValue(drawDto.DrawSystemId, out var existingDraw))
            {
                skipped++;
                continue;
            }

            _logger.LogDebug("{ClassName}: SaveDraw: Add draw for DrawTypeId {DrawTypeId}|{DrawSystemId}.", GetType().Name, drawTypeId, drawDto.DrawSystemId);

            var newDraw = new Draw
            {
                Id = 0,
                DrawTypeId = drawTypeId,
                DrawSystemId = drawDto.DrawSystemId,
                DrawDate = drawDto.DrawDate,
                Numbers = [.. drawDto.Numbers.OrderBy(n => n)],
                Specials = [.. drawDto.Specials.OrderBy(n => n)],
            };

            await dbContext.Draws.AddAsync(newDraw, stoppingToken);
            imported++;
            dbActionsCount++;

            // Save in batches to avoid large transactions
            if (dbActionsCount >= 1000)
            {
                await dbContext.SaveChangesAsync(stoppingToken);
                dbActionsCount = 0;
            }
        }

        if (dbActionsCount > 0)
        {
            await dbContext.SaveChangesAsync(stoppingToken);
        }

        _logger.LogDebug("{ClassName}: SaveDraws end. Imported: {Imported}, Updated: {Updated}, Skipped: {Skipped}",
            GetType().Name, imported, updated, skipped);

        return (imported, updated, skipped);
    }

    private record DrawDto(long DrawSystemId, DateTime DrawDate, List<int> Numbers, List<int> Specials);
}