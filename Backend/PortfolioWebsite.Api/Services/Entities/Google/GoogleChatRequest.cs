using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class GoogleChatRequest
{
    [JsonPropertyName("generationConfig")]
    public GenerationConfig GenerationConfig { get; set; }

    [JsonPropertyName("systemInstruction")]
    public Content? SystemInstruction { get; set; }

    [JsonPropertyName("contents")]
    public List<Content> Contents { get; set; }

    [JsonPropertyName("tools")]
    public Tools? Tools { get; set; }

    [JsonPropertyName("tool_config")]
    public ToolConfig? ToolConfig { get; set; }
}

public class UsageMetadata
{
    [JsonPropertyName("promptTokenCount")] public int PromptTokenCount { get; set; }
    [JsonPropertyName("candidatesTokenCount")] public int CandidatesTokenCount { get; set; }
    [JsonPropertyName("totalTokenCount")] public int TotalTokenCount { get; set; }
    [JsonPropertyName("promptTokensDetails")] public List<TokenDetails> PromptTokensDetails { get; set; }
    [JsonPropertyName("candidatesTokensDetails")] public List<TokenDetails> CandidatesTokensDetails { get; set; }
}
