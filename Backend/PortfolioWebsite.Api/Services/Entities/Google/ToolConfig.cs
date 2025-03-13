using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class ToolConfig
{
    [JsonPropertyName("function_calling_config")]
    public FunctionCallingConfig? FunctionCallingConfig { get; set; }
}
