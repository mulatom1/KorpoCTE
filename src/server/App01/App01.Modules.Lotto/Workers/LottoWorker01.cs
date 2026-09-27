using System.Text.Json;

using App01.Modules.Lotto.Services.LottoOpenApi;
using App01.Modules.Lotto.Services.LottoOpenApi.Dto;
using App01.Shared.Application.Entities.Lotto;
using App01.Shared.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Lotto.Workers;


public class LottoWorker01(
    ILogger<LottoWorker01> logger,
    IConfiguration configuration,
    IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private readonly ILogger<LottoWorker01> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("{ClassName}: Started.", GetType().Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = _configuration.GetValue("Workers:LottoWorker01:IntervalMinutes", 1.0);
            if (!_configuration.GetValue("Workers:LottoWorker01:Enabled", true))
            {
                _logger.LogDebug("{ClassName}: Service disabled.", GetType().Name);
                await Task.Delay((int)(interval * 1000 * 60), stoppingToken);
                continue;
            }

            var configStartTime = _configuration.GetValue("Workers:LottoWorker01:StartTime", "06:00:00");
            var configEndTime = _configuration.GetValue("Workers:LottoWorker01:EndTime", "23:59:59");
            var startTime = TimeSpan.Parse(configStartTime);
            var endTime = TimeSpan.Parse(configEndTime);
            var currentTime = DateTime.Now.TimeOfDay;
            if (currentTime < startTime || currentTime > endTime)
            {
                _logger.LogDebug("{ClassName}: Freezed.", GetType().Name);
                await Task.Delay((int)(interval * 1000 * 60), stoppingToken);
                continue;
            }

            using var scope = _serviceScopeFactory.CreateScope();

            var lottoService = scope.ServiceProvider.GetService<ILottoOpenApiService>()
                ?? throw new InvalidOperationException("LottoOpenApiService not available.");

            var dbContext = scope.ServiceProvider.GetService<AppDbContext>()
                ?? throw new InvalidOperationException("AppDbContext not available.");

            await Processing(lottoService, dbContext, DateTime.Today, stoppingToken);
            await Task.Delay(1000, stoppingToken);

            var intensiveBackupHours = _configuration.GetSection("Workers:LottoWorker01:IntensiveBackupHours").Get<int[]>() ?? [];
            if (intensiveBackupHours.Contains(currentTime.Hours))
            {
                await Processing(lottoService, dbContext, DateTime.Today.AddDays(-1), stoppingToken);
                await Task.Delay(1000, stoppingToken);

                await Processing(lottoService, dbContext, DateTime.Today.AddDays(-2), stoppingToken);
                await Task.Delay(1000, stoppingToken);

                await Processing(lottoService, dbContext, DateTime.Today.AddDays(-3), stoppingToken);
                await Task.Delay(1000, stoppingToken);
            }

            _logger.LogDebug("{ClassName}: Freezed.", GetType().Name);
            var intensiveEveningHours = _configuration.GetSection("Workers:LottoWorker01:IntensiveEveningHours").Get<int[]>() ?? [];
            if (intensiveEveningHours.Contains(currentTime.Hours))
                await Task.Delay(1000 * 60 * 5, stoppingToken);
            else
                await Task.Delay((int)(interval * 1000 * 60), stoppingToken);
        }
    }

    private async Task Processing(ILottoOpenApiService lottoService, AppDbContext dbContext, DateTime date, CancellationToken stoppingToken)
    {
        _logger.LogDebug("{ClassName}: Processing started.", GetType().Name);

        if (date < DateTime.Parse("1957-01-27")) return;

        var response = await lottoService.GetDrawsByDate(date);
        if (response == null || !response.Status || response.Content == null || response.Content == "")
        {
            _logger.LogDebug("{ClassName}: Processing: Response/list is null/empty", GetType().Name);
            return;
        }

        var respo = JsonSerializer.Deserialize<List<LottoDraw>>(response.Content, GetJsonSerializerOptions());
        if (respo == null || respo.Count == 0)
        {
            _logger.LogDebug("{ClassName}: Processing: No draws found in response.", GetType().Name);
            return;
        }

        await SaveResponseAsDraws(respo, dbContext, stoppingToken);
        _logger.LogDebug("{ClassName}: Processing end.", GetType().Name);
    }

    private async Task SaveResponseAsDraws(List<LottoDraw> response, AppDbContext dbContext, CancellationToken stoppingToken)
    {
        _logger.LogDebug("{ClassName}: SaveResponseAsDraws started.", GetType().Name);

        try
        {
            await SaveDraw(response, dbContext, "Lotto", "Lotto", 1, stoppingToken);
            await SaveDraw(response, dbContext, "Lotto", "LottoPlus", 2, stoppingToken);
            await SaveDraw(response, dbContext, "MiniLotto", "MiniLotto", 3, stoppingToken);
            await SaveDraw(response, dbContext, "EkstraPensja", "EkstraPensja", 4, stoppingToken);
            await SaveDraw(response, dbContext, "EkstraPensja", "EkstraPremia", 5, stoppingToken);
            await SaveDraw(response, dbContext, "EuroJackpot", "EuroJackpot", 6, stoppingToken);
            await SaveDraw(response, dbContext, "Szybkie600", "Szybkie600", 7, stoppingToken);
            await SaveDraw(response, dbContext, "Kaskada", "Kaskada", 8, stoppingToken);
            await SaveDraw(response, dbContext, "MultiMulti", "MultiMulti", 9, stoppingToken);
            await SaveDraw(response, dbContext, "Keno", "Keno", 10, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName}: SaveResponseAsDraws: {Message}", GetType().Name, ex.Message);
        }

        _logger.LogDebug("{ClassName}: SaveResponseAsDraws end.", GetType().Name);
    }

    private async Task SaveDraw(List<LottoDraw> drawsResponse, AppDbContext dbContext, string gameType1, string gameType2, int gameTypeId, CancellationToken stoppingToken)
    {
        _logger.LogDebug("{ClassName}: SaveDraw started. {GameType1}|{GameType2}|{GameTypeId}", GetType().Name, gameType1, gameType2, gameTypeId);

        if (drawsResponse == null || drawsResponse.Count == 0)
        {
            _logger.LogDebug("{ClassName}: SaveDraw: GetLastDrawsResponse is null or empty", GetType().Name);
            return;
        }

        if (!drawsResponse.Where(d => d.GameType == gameType1).Any())
        {
            _logger.LogDebug("{ClassName}: SaveDraw: No draws found for game type {GameType1}", GetType().Name, gameType1);
            return;
        }

        var drawType = await dbContext.DrawTypes.FirstOrDefaultAsync(dt => dt.Id == gameTypeId, stoppingToken);
        if (drawType == null)
        {
            _logger.LogDebug("{ClassName}: SaveDraw: drawType in DB is null or empty", GetType().Name);
            return;
        }

        foreach (var dr in drawsResponse.Where(d => d.GameType == gameType1).ToList())
        {
            if (dr == null || dr.Results == null || dr.Results.Count == 0)
            {
                _logger.LogDebug("{ClassName}: SaveDraw: No draws found for game type {GameType1}", GetType().Name, gameType1);
                continue;
            }

            if (!dr.Results.Where(d => d.GameType == gameType2).Any())
            {
                _logger.LogDebug("{ClassName}: SaveDraw: No draws found for game type {GameType2}", GetType().Name, gameType2);
                continue;
            }

            foreach (var draw in dr.Results.Where(d => d.GameType == gameType2).ToList())
            {
                if (draw.DrawDate == null)
                {
                    _logger.LogDebug("{ClassName}: SaveDraw: DrawDate is null for game type {GameType2}", GetType().Name, gameType2);
                    continue;
                }
                if (draw.DrawSystemId == null)
                {
                    _logger.LogDebug("{ClassName}: SaveDraw: DrawSystemId is null for game type {GameType2}", GetType().Name, gameType2);
                    continue;
                }

                // Szukaj po DrawTypeId + DrawSystemId (bez czasu) - DrawSystemId jest unikalny per typ gry
                var existingDraw = await dbContext.Draws
                    .FirstOrDefaultAsync(d => d.DrawTypeId == gameTypeId && d.DrawSystemId == draw.DrawSystemId.Value, stoppingToken);
                if (existingDraw != null)
                {
                    _logger.LogDebug("{ClassName}: SaveDraw: Draw already exists for game type {GameType2}, DrawSystemId {DrawSystemId}, DrawDate {DrawDate}. Skipping.",
                                     GetType().Name, gameType2, draw.DrawSystemId.Value, draw.DrawDate.Value);
                    continue;
                }

                _logger.LogDebug("{ClassName}: SaveDraw: Add draw for {GameType2}|{DrawSystemId}.", GetType().Name, gameType2, draw.DrawSystemId);


                var numbers = new List<int>();
                var specials = new List<int>();

                foreach (var number in draw.ResultsJson ?? [])
                    numbers.Add(number);

                if (gameType2 == "Kaskada")
                    foreach (var number in draw.ResultsJson ?? []) specials.Add(number);
                else if ((draw.SpecialResults?.Count ?? 0) > 0)
                    foreach (var number in draw.SpecialResults ?? []) specials.Add(number);

                var newDraw = new Draw
                {
                    Id = 0,
                    DrawTypeId = gameTypeId,
                    DrawSystemId = draw.DrawSystemId.Value,
                    DrawDate = draw.DrawDate.Value,
                    Numbers = [.. numbers.OrderBy(n => n)],
                    Specials = [.. specials.OrderBy(n => n)],
                };

                await dbContext.Draws.AddAsync(newDraw, stoppingToken);
                await dbContext.SaveChangesAsync(stoppingToken);

                _logger.LogDebug("{ClassName}: SaveDraw: Completed saving draw for {GameType2}|{DrawSystemId}.", GetType().Name, gameType2, draw.DrawSystemId);
            }

        }
    }

    private static JsonSerializerOptions GetJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }
}