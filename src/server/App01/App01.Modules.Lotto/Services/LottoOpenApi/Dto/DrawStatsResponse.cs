using System.Text.Json.Serialization;


namespace App01.Modules.Lotto.Services.LottoOpenApi.Dto;

/// <summary>
/// Represents the response for a single draw's prize information.
/// </summary>
public class DrawStatsResponse
{
    /// <summary>
    /// A dictionary of prize levels (keys: "1", "2", "3", "4") to their prize information.
    /// </summary>
    [JsonPropertyName("prizes")]
    public Dictionary<string, DrawStatsResponsePrizeInfo>? Prizes { get; set; }

    /// <summary>
    /// The date and time of the draw.
    /// </summary>
    [JsonPropertyName("drawDate")]
    public DateTime DrawDate { get; set; }

    /// <summary>
    /// The system ID of the draw.
    /// </summary>
    [JsonPropertyName("drawSystemId")]
    public int? DrawSystemId { get; set; }

    /// <summary>
    /// The type of the game (e.g., "Lotto", "LottoPlus", "SuperSzansa").
    /// </summary>
    [JsonPropertyName("gameType")]
    public string? GameType { get; set; }

    /// <summary>
    /// Indicates whether the prizes are empty.
    /// </summary>
    [JsonPropertyName("prizesEmpty")]
    public bool PrizesEmpty { get; set; }
}