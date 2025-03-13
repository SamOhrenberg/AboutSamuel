using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class Tools
{
    [JsonPropertyName("functionDeclarations")] public List<FunctionDeclaration> FunctionDeclarations { get; set; }
}
