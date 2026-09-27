using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using App01.Modules.Lotto.Services.LottoOpenApi;
using App01.Modules.Lotto.Services.LottoOpenApi.Dto;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using App01.Shared.Infrastructure.Repositories;


namespace App01.Modules.Lotto.Workers;


public class LottoWorker02(
    ILogger<LottoWorker02> logger,
    IConfiguration configuration,
    IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private readonly ILogger<LottoWorker02> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory; 

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("{ClassName}: Started.", GetType().Name);        

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = _configuration.GetValue("Workers:LottoWorker02:IntervalMinutes", 0.1);
            if (!_configuration.GetValue("Workers:LottoWorker02:Enabled", true))
            {
                _logger.LogDebug("{ClassName}: Service disabled.", GetType().Name);
                await Task.Delay(TimeSpan.FromMinutes(interval), stoppingToken);
                continue;
            }
            
            var date = GetDateToProcess();
            if (date == null)
            {
                var freezingMinutes = _configuration.GetValue("Workers:LottoWorker02:FreezingMinutes", 30.0);
                _logger.LogDebug("{ClassName}: No date to process. Freezing for {Minutes} minutes.", GetType().Name, freezingMinutes);
                await Task.Delay(TimeSpan.FromMinutes(freezingMinutes), stoppingToken);
                continue;
            }

            using var scope = _serviceScopeFactory.CreateScope();
            var lottoService = scope.ServiceProvider.GetRequiredService<ILottoOpenApiService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await Processing(date.Value, lottoService, dbContext, stoppingToken);

            _logger.LogDebug("{ClassName}: Freezed.", GetType().Name);
            await Task.Delay(TimeSpan.FromMinutes(interval), stoppingToken);
        }
    }

    private DateTime? GetDateToProcess()
    {
        _logger.LogInformation("{ClassName}: Get date to process - start", GetType().Name);

        try
        {
            var afterDate = _configuration.GetValue<DateTime>("Workers:LottoWorker02:AfterDate", new DateTime(DateTime.Today.Year, 1, 1));

            DateTime[] dates = [.. Enumerable.Range(0, (int)(DateTime.Today.AddDays(-1) - afterDate).TotalDays + 1)
           .Select(i => afterDate.AddDays(i))
           .Reverse()];

            var directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TMP", "LOTTO", "ARCHIWUM");
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            // Pomijamy daty z .ok (zakończone) lub .err (błąd API)
            var filesOk = Directory.GetFiles(directoryPath, "Draws_*.ok");
            var filesErr = Directory.GetFiles(directoryPath, "Draws_*.err");

            List<string> filesToSkip = [];
            filesToSkip.AddRange(filesOk);
            filesToSkip.AddRange(filesErr);

            foreach (var file in filesToSkip)
            {
                var fileDatePart = Path.GetFileNameWithoutExtension(file).Substring(6);
                if (DateTime.TryParseExact(fileDatePart, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime fileDate))
                    dates = dates.Where(d => d != fileDate).ToArray();
            }

            if (dates.Length == 0)
                return null;

            var dateToProcess = dates.First();

            _logger.LogInformation("{ClassName}: Date to process {Date} - end", GetType().Name, dateToProcess.ToString("yyyy-MM-dd"));
            return dateToProcess;
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "{ClassName}:  Date to process Exception: {Message}.", GetType().Name, ex.Message);
            return null;
        }
    }

    private async Task Processing(DateTime date, ILottoOpenApiService lottoService, AppDbContext dbContext, CancellationToken stoppingToken)
    {
        _logger.LogDebug("{ClassName}: Processing started for Draw {Date}.", GetType().Name, date.ToString("yyyy-MM-dd"));

        try
        {
            var directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TMP", "LOTTO", "ARCHIWUM");
            var fileName = $"Draws_{date:yyyyMMdd}";

            if (!Directory.Exists(directoryPath))
            {
                _logger.LogDebug("{ClassName}: Processing: Directory does not exist: {DirectoryPath}, creating it.", GetType().Name, directoryPath);
                Directory.CreateDirectory(directoryPath);
            }

            var errFilePath = Path.Combine(directoryPath, $"{fileName}.err");
            var jsonFilePath = Path.Combine(directoryPath, $"{fileName}.json");
            var okFilePath = Path.Combine(directoryPath, $"{fileName}.ok");

            if (File.Exists(okFilePath))
            {
                _logger.LogDebug("{ClassName}: Processing: Ok file already exists for date: {Date}", GetType().Name, date.ToString("yyyy-MM-dd"));
                return;
            }
            if (File.Exists(errFilePath))
            {
                _logger.LogDebug("{ClassName}: Processing: Error file already exists for date: {Date}", GetType().Name, date.ToString("yyyy-MM-dd"));
                return;
            }

            string? jsonContent;

            // Jeśli JSON istnieje - użyj go (regeneracja CSV)
            if (File.Exists(jsonFilePath))
            {
                _logger.LogDebug("{ClassName}: Processing: JSON exists, regenerating CSV for date: {Date}", GetType().Name, date.ToString("yyyy-MM-dd"));
                jsonContent = await File.ReadAllTextAsync(jsonFilePath, stoppingToken);
            }
            else
            {
                // Brak JSON - pobierz z API
                _logger.LogDebug("{ClassName}: Processing: No JSON for date: {Date}, fetching from API.", GetType().Name, date.ToString("yyyy-MM-dd"));

                var response = await lottoService.GetDrawsByDate(date);
                if (response == null)
                {
                    _logger.LogWarning("{ClassName}: Processing: No response from Lotto for date: {Date}", GetType().Name, date.ToString("yyyy-MM-dd"));
                    await File.WriteAllTextAsync(errFilePath, $"No response from Lotto for date: {date:yyyy-MM-dd}", stoppingToken);
                    return;
                }
                if (!response.Status)
                {
                    _logger.LogWarning("{ClassName}: Processing: Error response from Lotto for date: {Date}, Error: {Error}", GetType().Name, date.ToString("yyyy-MM-dd"), response.Error);
                    await File.WriteAllTextAsync(errFilePath, $"{response.Error}", stoppingToken);
                    return;
                }

                jsonContent = response.Content;
                await File.WriteAllTextAsync(jsonFilePath, jsonContent, stoppingToken);
            }

            await WriteCsvFiles(jsonContent, dbContext, stoppingToken);
            await File.WriteAllTextAsync(okFilePath, $"OK: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC", stoppingToken);

            _logger.LogInformation("{ClassName}: Processing: Files for date: {Date} saved", GetType().Name, date.ToString("yyyy-MM-dd"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName}: Processing: Exception for date: {Date}", GetType().Name, date.ToString("yyyy-MM-dd"));
        }
    }

    private async Task WriteCsvFiles(string? content, AppDbContext dbContext, CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(content))
        {
            _logger.LogWarning("{ClassName}: WriteCsvFiles: No content to write CSV files.", GetType().Name);
            return;
        }

        var draws = JsonSerializer.Deserialize<List<LottoDraw>>(content, GetJsonSerializerOptions());
        if (draws == null || draws.Count == 0)
        {
            _logger.LogWarning("{ClassName}: WriteCsvFiles: No draws found in content.", GetType().Name);
            return;
        }

        var directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TMP", "LOTTO", "CSV");
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogDebug("{ClassName}: WriteCsvFiles: Directory does not exist: {DirectoryPath}, creating it.", GetType().Name, directoryPath);
            Directory.CreateDirectory(directoryPath);
        }

        // Load DrawTypes from database: Name -> (Id, Name)
        var drawTypes = await dbContext.DrawTypes
            .AsNoTracking()
            .ToDictionaryAsync(dt => dt.Name, dt => (dt.Id, dt.Name), stoppingToken);

        // Group results by DrawTypeId
        var resultsByDrawType = new Dictionary<int, List<(string DrawTypeName, LottoDrawResult Result)>>();

        foreach (var draw in draws)
        {
            if (draw.GameType == null || draw.Results == null || draw.Results.Count == 0)
                continue;

            foreach (var result in draw.Results)
            {
                if (result.GameType == null || result.DrawSystemId == null || result.DrawDate == null)
                    continue;

                if (!drawTypes.TryGetValue(result.GameType, out var drawType))
                {
                    _logger.LogDebug("{ClassName}: WriteCsvFiles: Unknown game type: {GameType}.", GetType().Name, result.GameType);
                    continue;
                }

                if (!resultsByDrawType.ContainsKey(drawType.Id))
                    resultsByDrawType[drawType.Id] = [];

                resultsByDrawType[drawType.Id].Add((drawType.Name, result));
            }
        }

        // Write one CSV file per DrawTypeId
        foreach (var (drawTypeId, results) in resultsByDrawType)
        {
            if (results.Count == 0)
                continue;

            var drawTypeName = results[0].DrawTypeName;
            await WriteCsvFile(directoryPath, drawTypeId, drawTypeName, results.Select(r => r.Result).ToList(), stoppingToken);
        }
    }

    private async Task WriteCsvFile(string directoryPath, int drawTypeId, string drawTypeName, List<LottoDrawResult> results, CancellationToken stoppingToken)
    {
        var fileDateTime = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");

        // File name: Data_DrawTypeId_DrawTypeName_DateTime.csv
        var fileName = $"Data_{drawTypeId:D2}_{drawTypeName}_{fileDateTime}.csv";
        var filePath = Path.Combine(directoryPath, fileName);

        var csvLines = new List<string>();

        foreach (var result in results)
        {
            var drawSystemId = result.DrawSystemId!.Value;
            var drawDate = result.DrawDate!.Value;

            // CSV line: DrawSystemId(20), DrawDate(UTC), n1, n2, ..., s1, s2, ...
            var numbers = result.ResultsJson ?? [];
            var specials = drawTypeName == "Kaskada"
                ? (result.ResultsJson ?? [])
                : (result.SpecialResults ?? []);

            var csvParts = new List<string>
            {
                drawSystemId.ToString("D20"),
                drawDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            csvParts.AddRange(numbers.Select(n => n.ToString()));
            csvParts.AddRange(specials.Select(s => s.ToString()));

            csvLines.Add(string.Join(",", csvParts));
        }

        await File.WriteAllLinesAsync(filePath, csvLines, stoppingToken);
        _logger.LogDebug("{ClassName}: WriteCsvFile: Created CSV file: {FileName} with {Count} lines", GetType().Name, fileName, csvLines.Count);
    }

    private static JsonSerializerOptions GetJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }
}