using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class FunctionParameters
{
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("properties")] public Dictionary<string, FunctionProperty> Properties { get; set; }
}
