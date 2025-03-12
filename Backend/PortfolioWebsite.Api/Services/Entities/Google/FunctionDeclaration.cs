using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class FunctionDeclaration
{
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; }
    [JsonPropertyName("parameters")] public FunctionParameters Parameters { get; set; }
}
