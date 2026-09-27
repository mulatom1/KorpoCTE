using App01.Modules.Lotto.Services.LottoOpenApi.Dto;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;


namespace App01.Modules.Lotto.Services.LottoOpenApi;

public partial class LottoOpenApiService : ILottoOpenApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LottoOpenApiService> _logger;

    public LottoOpenApiService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LottoOpenApiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    private string GetUrl()
    {
        var url = _configuration.GetValue("LottoOpenApi:Url", "");
        if (string.IsNullOrEmpty(url)) throw new InvalidOperationException("GetUrl: LottoOpenApi URL is not configured!");
        
        return url;
    }
    private HttpClient CreateHttpClient()
    {
        var apiKey = _configuration.GetValue("LottoOpenApi:ApiKey", "");
        if(string.IsNullOrEmpty(apiKey)) throw new InvalidOperationException("CreateHttpClient: LottoOpenApi secret is not configured!");

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "tomsoft1.Api.LottoOpenApiService/1.0");
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        httpClient.DefaultRequestHeaders.Add("secret", Encoding.UTF8.GetString(Convert.FromBase64String(apiKey))); 

        return httpClient;
    }

    private static JsonSerializerOptions GetJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }


    public async Task<GameInfoResponse?> GetInfoByGameType(string gameType)
    {
        try
        {
            var url = GetUrl();
            var httpClient = CreateHttpClient();

            var apiUrl = $"{url}/api/open/v1/lotteries/info?gameType={gameType}";
            _logger.LogDebug("GetInfoByGameType: Fetching draws from Lotto Open API: {Url}", apiUrl);

            var response = await httpClient.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"GetInfoByGameType: Lotto Open API returned status: {response.StatusCode}");

            var jsonContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("GetInfoByGameType: Received response from Lotto Open API {JsonContent}", jsonContent);

            return JsonSerializer.Deserialize<GameInfoResponse>(jsonContent, GetJsonSerializerOptions());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GetInfoByGameType: HttpRequestException {Error}", $"{ex.Message} {ex.InnerException?.Message}".Trim());
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "GetInfoByGameType: Failed to parse JSON response. {Error}", $"{ex.Message} {ex.InnerException?.Message}".Trim());
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetInfoByGameType: Unexpected error while fetching draw results {Error}", $"{ex.Message} {ex.InnerException?.Message}".Trim());
            return null;
        }
    }

    public async Task<LottoResponse> GetDrawsByDate(DateTime date)
    {
        try
        {
            var url = GetUrl();
            var httpClient = CreateHttpClient();

            var apiUrl = $"{url}/api/open/v1/lotteries/draw-results/by-date?drawDate={date:yyyy-MM-dd}";
            _logger.LogDebug("GetDrawsByDate: Fetching draws from Lotto Open API: {Url}", apiUrl);

            var response = await httpClient.GetAsync(apiUrl);
            var jsonContent = await response.Content.ReadAsStringAsync();

            var resp = new LottoResponse()
            {
                Status = response.IsSuccessStatusCode,
                Code = response.StatusCode.ToString(),
                Content = response.IsSuccessStatusCode ? jsonContent : null,
                Error = response.IsSuccessStatusCode ? null : jsonContent,
            };

            _logger.LogDebug("GetDrawsByDate: Received response from Lotto Open API {JsonContent}", jsonContent);
            return resp;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GetDrawsByDate: HttpRequestException {Error}", $"{ex.Message} {ex.InnerException?.Message}".Trim());
            return new LottoResponse() 
            {
                Status = false,
                Code = "HttpRequestException",
                Content = null,
                Error = $"{ex.Message} {ex.InnerException?.Message}".Trim(),
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "GetDrawsByDate: Failed to parse JSON response. {Error}", $"{ex.Message} {ex.InnerException?.Message}".Trim());
            return new LottoResponse()
            {
                Status = false,
                Code = "JsonException",
                Content = null,
                Error = $"{ex.Message} {ex.InnerException?.Message}".Trim(),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDrawsByDate: Unexpected error while fetching draw results {Error}", $"{ex.Message} {ex.InnerException?.Message}".Trim());
            return new LottoResponse()
            {
                Status = false,
                Code = "Exception",
                Content = null,
                Error = $"{ex.Message} {ex.InnerException?.Message}".Trim(),
            };
        }
    }

    public async Task<List<DrawStatsResponse>?> GetDrawPrizes(string drawType, long drawSystemId)
    {
        try
        {
            var url = GetUrl();
            var httpClient = CreateHttpClient();

            var apiUrl = $"{url}/api/open/v1/lotteries/draw-prizes/{drawType}/{drawSystemId}";
            _logger.LogDebug("Fetching stats of draw from Lotto Open API: {Url}", apiUrl);

            var response = await httpClient.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Lotto Open API returned status: {response.StatusCode}");

            var jsonContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("GetDrawsStatsById: Received response from Lotto Open API {JsonContent}", jsonContent);

            return JsonSerializer.Deserialize<List<DrawStatsResponse>>(jsonContent, GetJsonSerializerOptions());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GetDrawsStatsById: HttpRequestException {Error}", $"{ex.Message} {ex.InnerException?.Message}".Trim());
            return [];
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "GetDrawsStatsById: Failed to parse JSON response. {Error}", $"{ex.Message} {ex.InnerException?.Message}".Trim());
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDrawsStatsById: Unexpected error while fetching draw results {Error}", $"{ex.Message} {ex.InnerException?.Message}".Trim());
            return [];
        }
    }

    public async Task<List<LottoDraw>?> GetLastDraws()
    {
        try
        {
            var url = GetUrl();
            var httpClient = CreateHttpClient();

            var apiUrl = $"{url}/api/open/v1/lotteries/draw-results/last-results";
            _logger.LogDebug("Fetching draws from Lotto Open API: {Url}", apiUrl);

            var response = await httpClient.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"GetLastDraws: Lotto Open API returned status: {response.StatusCode}");

            var jsonContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("GetLastDraws: Received response from Lotto Open API {JsonContent}", jsonContent);

            return JsonSerializer.Deserialize<List<LottoDraw>?>(jsonContent, GetJsonSerializerOptions());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GetLastDraws: HttpRequestException {Error}", $"{ex.Message} {ex.InnerException?.Message}".Trim());
            return [];
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "GetLastDraws: Failed to parse JSON response. {Error}", $"{ex.Message} {ex.InnerException?.Message}".Trim());
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLastDraws: Unexpected error while fetching draw results {Error}", $"{ex.Message} {ex.InnerException?.Message}".Trim());
            return [];
        }
    }
}
