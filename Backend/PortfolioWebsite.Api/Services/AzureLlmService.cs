using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace PortfolioWebsite.Api.Services;

public class AzureLlmService : ILlmService
{
    private readonly ChatClient _toolUseClient;
    private readonly ChatClient _questionClient;
    private readonly ILogger<AzureLlmService> _logger;

    public AzureLlmService(IConfiguration configuration, ILogger<AzureLlmService> logger)
    {
        _logger = logger;

        var endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint must be configured.");
        var apiKey = configuration["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey must be configured.");

        var toolModel = configuration["ChatSettings:ToolUse:Model"] ?? "gpt-4o-mini";
        var questionModel = configuration["ChatSettings:Question:Model"] ?? "gpt-4o-mini";

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _toolUseClient = azureClient.GetChatClient(toolModel);
        _questionClient = azureClient.GetChatClient(questionModel);
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        IReadOnlyList<LlmMessage> messages,
        int maxTokens,
        CancellationToken ct = default)
    {
        var chatMessages = BuildMessages(systemPrompt, messages);
        var options = new ChatCompletionOptions { MaxOutputTokenCount = maxTokens };

        var result = await _questionClient.CompleteChatAsync(chatMessages, options, ct);
        return result.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        IReadOnlyList<LlmMessage> messages,
        int maxTokens,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var chatMessages = BuildMessages(systemPrompt, messages);
        var options = new ChatCompletionOptions { MaxOutputTokenCount = maxTokens };

        await foreach (var update in _questionClient.CompleteChatStreamingAsync(chatMessages, options, ct))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                    yield return part.Text;
            }
        }
    }

    public async Task<LlmResponse> CompleteWithToolsAsync(
        string systemPrompt,
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        int maxTokens,
        CancellationToken ct = default)
    {
        var chatMessages = BuildMessages(systemPrompt, messages);

        var options = new ChatCompletionOptions { MaxOutputTokenCount = maxTokens };
        foreach (var tool in tools)
        {
            options.Tools.Add(ChatTool.CreateFunctionTool(
                tool.Name,
                tool.Description,
                BinaryData.FromObjectAsJson(tool.InputSchema)));
        }

        var result = await _toolUseClient.CompleteChatAsync(chatMessages, options, ct);
        var completion = result.Value;

        if (completion.FinishReason == ChatFinishReason.ToolCalls)
        {
            var toolCalls = completion.ToolCalls
                .Select(tc => new LlmToolCall(tc.Id, tc.FunctionName, tc.FunctionArguments.ToString()))
                .ToList();

            return LlmResponse.WithTools(toolCalls, completion.Usage?.TotalTokenCount ?? 0);
        }

        var text = completion.Content.FirstOrDefault()?.Text ?? string.Empty;
        return LlmResponse.TextOnly(text, completion.Usage?.TotalTokenCount ?? 0);
    }


    private static List<ChatMessage> BuildMessages(string systemPrompt, IReadOnlyList<LlmMessage> messages)
    {
        var result = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt)
        };

        foreach (var msg in messages)
        {
            result.Add(msg.Role == "user"
                ? new UserChatMessage(msg.Content)
                : new AssistantChatMessage(msg.Content));
        }

        return result;
    }
}
