using App01.Shared.Application.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Portal.Workers;


public class PortalWorker01(
    ILogger<PortalWorker01> logger,
    IConfiguration configuration,
    IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private readonly ILogger<PortalWorker01> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("ApiWorker01Ping: Started.");
        
        DateTime currentTime;


        while (!stoppingToken.IsCancellationRequested)
        {
            currentTime = DateTime.Now;

            try
            {
                using var scope = _serviceScopeFactory.CreateScope();

                var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient();

                var url = _configuration.GetValue<string>("ApiUrl") ?? throw new ApiException("API URL is not configured. Skipping ping.");

                _logger.LogDebug("ApiWorker01Ping: {Url}/api/version to keep the website alive", url);


                var response = await httpClient.GetAsync($"{url}/api/portal/get-api-version", stoppingToken);

                if (response.IsSuccessStatusCode)
                    _logger.LogDebug("ApiWorker01Ping: Successfully pinged Status: {StatusCode}", response.StatusCode);
                else
                    _logger.LogDebug("ApiWorker01Ping: Ping to returned non-success status: {StatusCode}", response.StatusCode);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "ApiWorker01Ping: HTTP request exception while pinging PingForApiVersion. {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApiWorker01Ping: Unexpected error - {Message}", ex.Message);
            }

            var nextRunTime = currentTime.AddMinutes(_configuration.GetValue<double>("Workers:ApiWorker01Ping:IntervalMinutes", 5));
            var freeze = nextRunTime - DateTime.Now;
            if (freeze < TimeSpan.Zero) freeze = TimeSpan.Zero;
            
            _logger.LogDebug("ApiWorker01Ping: Freezed. Next run scheduled at {NextRunTime}. Waiting for {Delay} minutes.", nextRunTime, freeze.TotalMinutes);
            
            await Task.Delay((int)freeze.TotalMilliseconds, stoppingToken);
        }
    }
}