using System.Text;
using System.Text.Json;

using App01.Shared.Application.Interfaces;
using App01.Shared.Application.Models.AI;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace App01.Bootstrapper.Api.Services;

public class OpenRouterService : IOpenRouterService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenRouterService> _logger;

    public OpenRouterService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenRouterService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> ChatAsync(string? model, IList<ChatMessage> historyMessages, ChatMessage prompt, CancellationToken cancellationToken = default)
    {
        var openRouterUrl = _configuration["OpenRouter:Url"];
        var openRouterApiKey = _configuration["OpenRouter:ApiKey"];
        var openRouterModel = _configuration["OpenRouter:Model"];

        if (string.IsNullOrEmpty(openRouterUrl) || string.IsNullOrEmpty(openRouterApiKey))
        {
            throw new Exception("OpenRouter configuration is missing");
        }

        string decodedApiKey;
        try
        {
            var base64Bytes = Convert.FromBase64String(openRouterApiKey.Trim());
            decodedApiKey = Encoding.UTF8.GetString(base64Bytes);
            _logger.LogDebug("Successfully decoded OpenRouter API key");
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Failed to decode OpenRouter API key from Base64. Key length: {Length}", openRouterApiKey.Length);
            throw new Exception("OpenRouter API key is not a valid Base64 string", ex);
        }

        var messages = historyMessages
            .Select(m => new { role = m.Role, content = m.Content })
            .Append(new { role = prompt.Role, content = prompt.Content })
            .ToList<object>();

        var requestBody = new
        {
            model = model ?? openRouterModel,
            messages
        };

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {decodedApiKey}");

        var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var httpResponse = await httpClient.PostAsync(openRouterUrl, jsonContent, cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("OpenRouter API error: {StatusCode} - {Error}", httpResponse.StatusCode, errorContent);
            throw new Exception($"OpenRouter API error: {httpResponse.StatusCode}");
        }

        var responseContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(responseContent);
        var contentText = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        _logger.LogDebug("OpenRouter response: {Content}", contentText);

        return contentText ?? "";
    }
}