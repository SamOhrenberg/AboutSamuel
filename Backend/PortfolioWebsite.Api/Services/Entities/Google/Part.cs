using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class Part
{
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("functionCall")] public FunctionCallResponse? FunctionCall { get; set; }
}
