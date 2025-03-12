using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class GenerationConfig
{
    [JsonPropertyName("temperature")] public double Temperature { get; set; }
    [JsonPropertyName("topK")] public int TopK { get; set; }
    [JsonPropertyName("topP")] public double TopP { get; set; }
    [JsonPropertyName("maxOutputTokens")] public int MaxOutputTokens { get; set; }
    [JsonPropertyName("responseMimeType")] public string ResponseMimeType { get; set; }
}
