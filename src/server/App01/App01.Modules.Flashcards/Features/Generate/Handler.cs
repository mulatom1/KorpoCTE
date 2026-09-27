using System.Text;
using System.Text.Json;

using App01.Shared.Application.Entities.Lotto;
using App01.Shared.Application.Exceptions;
using App01.Shared.Application.Interfaces;
using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Flashcards.Features.Generate;


public class Handler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<Handler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly IJwtService _jwtService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;


    public Handler(
        ILogger<Handler> logger,
        IValidator<Contracts.Request> validator,
        IJwtService jwtService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _validator = validator;
        _jwtService = jwtService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // Only logged in users can generate flashcards
        var userEmail = await _jwtService.GetEmailFromJwt();
        if ((userEmail ?? "").Trim() == "")
        {
            throw new ForbiddenException("Tylko zalogowani użytkownicy mogą generować fiszki!");
        }

        // Get OpenRouter configuration
        var openRouterUrl = _configuration["OpenRouter:Url"];
        var openRouterApiKey = _configuration["OpenRouter:ApiKey"];
        var openRouterModel = _configuration["OpenRouter:Model"];

        if (string.IsNullOrEmpty(openRouterUrl) || string.IsNullOrEmpty(openRouterApiKey))
        {
            throw new Exception("OpenRouter configuration is missing");
        }

        // Decode Base64 API key
        string decodedApiKey;
        try
        {
            var trimmedApiKey = openRouterApiKey.Trim();
            var base64Bytes = Convert.FromBase64String(trimmedApiKey);
            decodedApiKey = Encoding.UTF8.GetString(base64Bytes);
            _logger.LogDebug("Successfully decoded OpenRouter API key");
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Failed to decode OpenRouter API key from Base64. Key length: {Length}", openRouterApiKey?.Length ?? 0);
            throw new Exception("OpenRouter API key is not a valid Base64 string", ex);
        }

        // Prepare prompt for OpenRouter
        var prompt = $@"<tresc_dla_fiszek>{request.Text}</tresc_dla_fiszek>
                      Na podstawie treści dla fiszek wygeneruj {request.Count} fiszek w strukturze JSON:
                      [{{""question"":""answer""}}]
                      
                      Zamiast [question] wstaw pełne pytanie, a zamiast [answer] pełną odpowiedź. Odpowiedzi powinny być krótkie i zwięzłe.
                      Oddaj tylko poprawną strukture JSON i nic po za tym, bez wzmianki ze to JSON.";

        // Call OpenRouter API
        var httpClient = _httpClientFactory.CreateClient();
        var requestBody = new
        {
            model = openRouterModel,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            }
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {decodedApiKey}");

        var httpResponse = await httpClient.PostAsync(openRouterUrl, httpContent, cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("OpenRouter API error: {StatusCode} - {Error}", httpResponse.StatusCode, errorContent);
            throw new Exception($"OpenRouter API error: {httpResponse.StatusCode}");
        }

        var responseContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        // Parse OpenRouter response
        using var doc = JsonDocument.Parse(responseContent);
        var contentText = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrEmpty(contentText))
        {
            throw new Exception("Empty response from OpenRouter");
        }

        // Parse flashcards JSON
        var flashcardsJson = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(contentText);

        if (flashcardsJson == null || flashcardsJson.Count == 0)
        {
            throw new Exception("Failed to parse flashcards from OpenRouter response");
        }

        // Convert to List<FlashcardItem>
        var flashcards = new List<Contracts.FlashcardItem>();
        foreach (var flashcard in flashcardsJson)
        {
            foreach (var kvp in flashcard)
            {
                flashcards.Add(new Contracts.FlashcardItem(kvp.Key, kvp.Value));
            }
        }

        var response = new Contracts.Response(
            Flashcards: flashcards,
            GeneratedCount: flashcards.Count,
            InputTextLength: request.Text.Length
        );

        _logger.LogDebug("Generated {Count} flashcards for user {UserEmail}", response.GeneratedCount, userEmail);

        return response;
    }
}