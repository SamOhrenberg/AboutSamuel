using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class FunctionCallResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("args")]
    public Dictionary<string, string>? Args { get; set; }
}