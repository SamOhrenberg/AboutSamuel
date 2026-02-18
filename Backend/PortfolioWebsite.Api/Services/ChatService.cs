using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime.Documents;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos;
using PortfolioWebsite.Api.Services.Entities;
using PortfolioWebsite.Common;
using System.Diagnostics;
using System.Text;

namespace PortfolioWebsite.Api.Services;

public class ChatService
{
    private const int MatchWeight = 60;

    private readonly ILogger<ChatService> _logger;
    private readonly SqlDbContext _dbContext;
    private readonly IAmazonBedrockRuntime _bedrockClient;
    private readonly ContactService _contactService;

    private record ModelSettings(string Model);
    private readonly ModelSettings _toolUse;
    private readonly ModelSettings _questions;

    public ChatService(
        IConfiguration configuration,
        ILogger<ChatService> logger,
        SqlDbContext dbContext,
        IAmazonBedrockRuntime bedrockClient,
        ContactService contactService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _bedrockClient = bedrockClient;
        _contactService = contactService;

        string toolModel = configuration.GetValue<string>("ChatSettings:ToolUse:Model")
            ?? throw new NullReferenceException("ChatSettings:ToolUse:Model must be populated in settings");
        _toolUse = new ModelSettings(toolModel);

        string questionModel = configuration.GetValue<string>("ChatSettings:Question:Model")
            ?? throw new NullReferenceException("ChatSettings:Question:Model must be populated in settings");
        _questions = new ModelSettings(questionModel);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Public Entry Points
    // ═════════════════════════════════════════════════════════════════════════

    internal async Task<ChatResponse> QueryChat(ChatLog chat)
    {
        var receivedAt = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();

        var response = await QueryBedrock(chat);

        stopwatch.Stop();
        _logger.LogInformation("Chat query took {Milliseconds}ms", stopwatch.ElapsedMilliseconds);

        await SaveChatLog(
            chat,
            response.Message,
            response.Error,
            response.TokenLimitReached,
            receivedAt,
            stopwatch.ElapsedMilliseconds);

        return response;
    }

    public async Task<string?> GenerateHtmlResume(string? title)
    {
        var allInformation = await _dbContext.Information.OrderBy(t => t.Text).ToListAsync();
        var textBlock = string.Join(
            "\r\n\r\nNew Information:\r\n",
            allInformation.Select(i => i.Text));

        var titleInstruction = !string.IsNullOrEmpty(title)
            ? $"Personalize the resume for a '{title}' role, retaining only relevant and pertinent details."
            : string.Empty;

        var systemPrompt = $$"""
            You are a professional resume generator. I will provide raw information about Samuel Ohrenberg's 
            professional experience, education, and skills. Generate an HTML resume that meets these requirements:

            - Slots within a Vue.js <template></template> (do NOT include the <template> tags themselves)
            - Does NOT include a contact section
            - Uses ONLY inline style attributes — no <style> tags, no CSS class names
            - Includes ALL jobs, ALL projects, education, and important skills
            - Is visually appealing, accessible (screen-reader friendly), and responsive
            - Has strong contrast between foreground text and background colors
            - Paraphrases and summarizes as needed while retaining all important details

            Use this color theme throughout:
                background:           #e6eeee
                surface:              #FFFFFF
                surface-light:        #EEEEEE
                surface-variant:      #424242
                on-surface-variant:   #EEEEEE
                primary:              #ecf2f2
                primary-darken-1:     #1F5592
                secondary:            #48A9A6
                secondary-darken-1:   #018786
                error:                #B00020
                info:                 #2196F3
                success:              #4CAF50
                warning:              #FB8C00
                accent:               #006a6a
                yellow:               #eeae31

            {{titleInstruction}}

            You MUST output ONLY a valid JSON object in exactly this format with no markdown, no code fences, and no extra text:
            {"html": "<your complete html string here>"}
            """;

        var request = new ConverseRequest
        {
            ModelId = _questions.Model,
            System = [new SystemContentBlock { Text = systemPrompt }],
            Messages =
            [
                new Message
                {
                    Role = "user",
                    Content = [new ContentBlock { Text = textBlock }]
                }
            ],
            InferenceConfig = new InferenceConfiguration { MaxTokens = 8192 }
        };

        var result = await _bedrockClient.ConverseAsync(request);
        var raw = result.Output.Message.Content
            .FirstOrDefault(c => c.Text != null)?.Text ?? string.Empty;

        // Strip any accidental markdown code fences
        raw = raw.Trim();
        if (raw.StartsWith("```"))
        {
            raw = string.Join('\n', raw.Split('\n').Skip(1));
            if (raw.TrimEnd().EndsWith("```"))
                raw = raw[..raw.LastIndexOf("```")].TrimEnd();
        }

        try
        {
            var jObj = JObject.Parse(raw);
            return jObj["html"]?.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resume response was not valid JSON — returning raw content");
            return raw;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Bedrock Core: Tool-Use Routing
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<ChatResponse> QueryBedrock(ChatLog chat)
    {
        const string systemPrompt = """
            You are SamuelLM, an AI chatbot created by Samuel Ohrenberg (also known as Sam or Sammy).
            Your website is https://aboutsamuel.com/.
            You are hosted on Sam's infrastructure as an AI assistant for his portfolio website,
            built with an ASP.NET Core Web API and a Vue.js front end.
            You answer on behalf of Samuel — respond as if you were him in a professional interview setting.
            You are professional, friendly, and helpful.
            You must always select the most appropriate tool based on the user's message.
            Do not output markdown. Use plain text only.
            """;

        // Build conversation history
        var messages = chat.History
            .Select(h => new Message
            {
                Role = h.Role == "user" ? "user" : "assistant",
                Content = [new ContentBlock { Text = h.Content }]
            })
            .ToList();

        messages.Add(new Message
        {
            Role = "user",
            Content = [new ContentBlock { Text = chat.Message }]
        });

        var request = new ConverseRequest
        {
            ModelId = _toolUse.Model,
            System = [new SystemContentBlock { Text = systemPrompt }],
            Messages = messages,
            ToolConfig = new ToolConfiguration
            {
                Tools = GetToolDefinitions(),
                // Force the model to always select a tool (mirrors Gemini's ANY mode)
                ToolChoice = new ToolChoice { Any = new AnyToolChoice() }
            },
            InferenceConfig = new InferenceConfiguration { MaxTokens = 1000 }
        };

        ConverseResponse result;
        try
        {
            result = await _bedrockClient.ConverseAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Bedrock Converse API");
            return new ChatResponse("I'm sorry, I encountered an error. Please try again.", true);
        }

        bool tokenLimitReached = (result.Usage?.TotalTokens ?? 0) > 2500;
        string message = string.Empty;
        bool error = false;
        bool returnResume = false;
        string? redirectToPage = null;

        foreach (var block in result.Output.Message.Content)
        {
            // Capture any inline text the model produced alongside tool calls
            if (block.Text != null)
                message = block.Text;

            if (block.ToolUse == null)
                continue;

            var toolName = block.ToolUse.Name;
            var toolInput = block.ToolUse.Input;

            _logger.LogInformation("Tool selected: {ToolName}", toolName);

            switch (toolName)
            {
                case "contactSamuel":
                    {
                        var email = GetStringArg(toolInput, "email");
                        var msg = GetStringArg(toolInput, "message");
                        string? contactError = null;

                        try
                        {
                            if (!string.IsNullOrEmpty(email))
                                await _contactService.SendContactRequest(email, msg);
                            else
                                contactError = "No email address was provided.";
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error sending contact request");
                            contactError = "An unknown system error occurred while sending the request.";
                        }

                        message = await GetContactConfirmationMessage(email, msg, contactError);
                        break;
                    }

                case "getResume":
                    returnResume = true;
                    message = "Of course! Here is Sam's resume!";
                    break;

                case "redirectToPage":
                    {
                        redirectToPage = GetStringArg(toolInput, "page");

                        // Also generate a short contextual message to accompany the redirect
                        var questionResponse = await AskBedrockQuestion(messages, systemPrompt);
                        message = questionResponse.Message;
                        error = error || questionResponse.Error;
                        tokenLimitReached = tokenLimitReached || questionResponse.TokenLimitReached;
                        break;
                    }

                case "askQuestion":
                    {
                        var questionResponse = await AskBedrockQuestion(messages, systemPrompt);
                        message = questionResponse.Message;
                        error = error || questionResponse.Error;
                        tokenLimitReached = tokenLimitReached || questionResponse.TokenLimitReached;
                        break;
                    }

                default:
                    _logger.LogWarning("Unrecognized tool call: {ToolName}", toolName);
                    break;
            }
        }

        // Fallback if the model returned text but no tool call (shouldn't happen in ANY mode, but be safe)
        if (string.IsNullOrWhiteSpace(message))
            message = "I'm sorry, I wasn't sure how to handle that. Could you rephrase your question?";

        return new ChatResponse(message, error, tokenLimitReached, returnResume, redirectToPage);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Bedrock: Q&A with RAG (Relevant Information Lookup)
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<ChatResponse> AskBedrockQuestion(List<Message> conversationHistory, string baseSystemPrompt)
    {
        // Gather all user-turn text so we can extract keywords for RAG lookup
        var userTextBuilder = new StringBuilder();
        foreach (var msg in conversationHistory.Where(m => m.Role == "user"))
        {
            foreach (var block in msg.Content.Where(c => c.Text != null))
                userTextBuilder.Append(block.Text).Append(' ');
        }

        var tokens = Tokenizer.Tokenize(userTextBuilder.ToString());
        var relevantInfo = await GetRelevantInformation(tokens);

        if (string.IsNullOrWhiteSpace(relevantInfo))
        {
            // Log what was asked so the gap can be filled later
            await CreateInformationRequest(tokens);
            return new ChatResponse(
                "I'm sorry, I don't have information about that topic yet. " +
                "I've made a note of the gap and will work to get it added!",
                false);
        }

        var enrichedSystemPrompt = $"""
            {baseSystemPrompt}

            Use the following curated excerpts from Samuel Ohrenberg's professional background to answer the user's question.
            There may be additional relevant details not shown — these are the most pertinent sections:

            {relevantInfo}

            Respond concisely and professionally, as if you are Samuel in an interview. Keep your answer under 150 words.
            Do not output markdown. Use plain text only.
            """;

        var request = new ConverseRequest
        {
            ModelId = _questions.Model,
            System = [new SystemContentBlock { Text = enrichedSystemPrompt }],
            Messages = conversationHistory,
            InferenceConfig = new InferenceConfiguration { MaxTokens = 300 }
        };

        try
        {
            var result = await _bedrockClient.ConverseAsync(request);
            var text = result.Output.Message.Content
                .FirstOrDefault(c => c.Text != null)?.Text ?? string.Empty;
            bool tokenLimitReached = (result.Usage?.TotalTokens ?? 0) > 2500;
            return new ChatResponse(text, false, tokenLimitReached);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Bedrock for Q&A response");
            return new ChatResponse(
                "I encountered an error while generating a response. Please try again.",
                true);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Bedrock: Contact Confirmation Message Generation
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<string> GetContactConfirmationMessage(string? email, string? msg, string? error)
    {
        const string systemPrompt = """
            You are an AI assistant on Samuel Ohrenberg's portfolio website, aboutsamuel.com.
            A user has just submitted a contact request form. Generate a short, friendly confirmation message 
            to display in the chat window — addressed to the user.
            If an error occurred, acknowledge it politely and suggest they try again or reach out directly.
            Output only the message text. No labels, no JSON, no extra formatting. Keep it under 60 words.
            """;

        var userContent = $"""
            Email provided: {email ?? "(not provided)"}
            Message from user: {msg ?? "(none)"}
            Result: {(string.IsNullOrEmpty(error) ? "Successfully received and sent to Samuel." : $"Error — {error}")}
            """;

        var request = new ConverseRequest
        {
            ModelId = _questions.Model,
            System = [new SystemContentBlock { Text = systemPrompt }],
            Messages =
            [
                new Message
                {
                    Role = "user",
                    Content = [new ContentBlock { Text = userContent }]
                }
            ],
            InferenceConfig = new InferenceConfiguration { MaxTokens = 150 }
        };

        try
        {
            var result = await _bedrockClient.ConverseAsync(request);
            return result.Output.Message.Content
                .FirstOrDefault(c => c.Text != null)?.Text
                ?? "Thank you! Your contact request has been received. Samuel will be in touch soon!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating contact confirmation message");
            return "Your contact request was received! Samuel will be in touch soon.";
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Tool Definitions (Bedrock Converse API Format)
    // ═════════════════════════════════════════════════════════════════════════

    private static List<Tool> GetToolDefinitions()
    {
        return
        [
            // ── contactSamuel ────────────────────────────────────────────────
            new Tool
            {
                ToolSpec = new ToolSpecification
                {
                    Name = "contactSamuel",
                    Description =
                        "Sends a contact request to Samuel Ohrenberg on behalf of the user, using their " +
                        "email address and an optional message. Use this when the user explicitly wants to " +
                        "get in touch with Sam, request a follow-up, or reach out directly.",
                    InputSchema = new ToolInputSchema
                    {
                        Json = new Document(new Dictionary<string, Document>
                        {
                            ["type"] = new Document("object"),
                            ["properties"] = new Document(new Dictionary<string, Document>
                            {
                                ["email"] = new Document(new Dictionary<string, Document>
                                {
                                    ["type"]        = new Document("string"),
                                    ["description"] = new Document(
                                        "The user's email address so Samuel can respond. " +
                                        "This must be explicitly provided by the user — do not infer or fabricate it.")
                                }),
                                ["message"] = new Document(new Dictionary<string, Document>
                                {
                                    ["type"]        = new Document("string"),
                                    ["description"] = new Document(
                                        "An optional short message from the user explaining the reason for their contact request.")
                                })
                            }),
                            ["required"] = new Document(new List<Document> { new Document("email") })
                        })
                    }
                }
            },

            // ── redirectToPage ───────────────────────────────────────────────
            new Tool
            {
                ToolSpec = new ToolSpecification
                {
                    Name = "redirectToPage",
                    Description =
                        "Redirects the user to a relevant page on the website when their query is best " +
                        "answered by navigating them there directly. Available pages:\n" +
                        "  - 'Contact':     A form the user can fill out to reach out to Samuel.\n" +
                        "  - 'Testimonial': Testimonials from Samuel's coworkers and professional partners.\n" +
                        "  - 'Resume':      Samuel's full work history, projects, education, and skills.\n" +
                        "Use this when the user is asking for something that lives on one of these pages " +
                        "and would benefit most from going there.",
                    InputSchema = new ToolInputSchema
                    {
                        Json = new Document(new Dictionary<string, Document>
                        {
                            ["type"] = new Document("object"),
                            ["properties"] = new Document(new Dictionary<string, Document>
                            {
                                ["page"] = new Document(new Dictionary<string, Document>
                                {
                                    ["type"]        = new Document("string"),
                                    ["description"] = new Document(
                                        "The page to redirect the user to. Must be exactly one of: 'Contact', 'Testimonial', 'Resume'.")
                                })
                            }),
                            ["required"] = new Document(new List<Document> { new Document("page") })
                        })
                    }
                }
            },

            // ── getResume ────────────────────────────────────────────────────
            new Tool
            {
                ToolSpec = new ToolSpecification
                {
                    Name = "getResume",
                    Description =
                        "Returns Samuel Ohrenberg's resume for the user to view or download. " +
                        "Use this when the user explicitly asks to see, view, or download Sam's resume or CV.",
                    InputSchema = new ToolInputSchema
                    {
                        // No parameters — empty object schema
                        Json = new Document(new Dictionary<string, Document>
                        {
                            ["type"]       = new Document("object"),
                            ["properties"] = new Document(new Dictionary<string, Document>())
                        })
                    }
                }
            },

            // ── askQuestion ──────────────────────────────────────────────────
            new Tool
            {
                ToolSpec = new ToolSpecification
                {
                    Name = "askQuestion",
                    Description =
                        "Answers any question about Samuel Ohrenberg. This includes technical questions, " +
                        "interview-style questions, questions about his experience, skills, personal background, " +
                        "projects, education, or professional history. Use this as the default tool for any " +
                        "informational or conversational query that does not require a redirect or a contact request.",
                    InputSchema = new ToolInputSchema
                    {
                        Json = new Document(new Dictionary<string, Document>
                        {
                            ["type"] = new Document("object"),
                            ["properties"] = new Document(new Dictionary<string, Document>
                            {
                                ["question"] = new Document(new Dictionary<string, Document>
                                {
                                    ["type"]        = new Document("string"),
                                    ["description"] = new Document(
                                        "The full question being asked by the user, e.g. " +
                                        "'What programming languages does Sam know?' or " +
                                        "'Tell me about his experience with microservices.'")
                                })
                            }),
                            ["required"] = new Document(new List<Document> { new Document("question") })
                        })
                    }
                }
            }
        ];
    }

    // ═════════════════════════════════════════════════════════════════════════
    // RAG: Relevant Information Lookup
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<string> GetRelevantInformation(IEnumerable<string> tokens)
    {
        var informations = await _dbContext.Information
            .Include(i => i.Keywords)
            .ToListAsync();

        // Augment stored keywords with tokens derived from the text itself (in-memory)
        foreach (var info in informations)
        {
            info.Keywords.AddRange(
                Tokenizer.Tokenize(info.Text)
                    .Select(t => new Keyword(t, info)));
            info.Keywords = info.Keywords.DistinctBy(k => k.Text).ToList();
        }

        // Score and down-select when there are more results than the LLM context can handle
        if (informations.Count > 3)
        {
            var counted = informations.Select(i => new
            {
                Information = i,
                NumberOfMatches = i.Keywords.Count(k =>
                    tokens.Any(token => Utility.LevenshteinDifference(k.Text.ToLower(), token) <= 30))
            }).ToList();

            float max = counted.Max(a => a.NumberOfMatches);
            float min = counted.Min(a => a.NumberOfMatches);

            // Avoid division-by-zero when all items have the same match count
            var scored = counted.Select(i => new
            {
                Information = i.Information,
                Score = max == min
                    ? (float)Utility.TrueRandom(1, 100 - MatchWeight)
                    : ((i.NumberOfMatches - min) / (max - min) * MatchWeight)
                      + Utility.TrueRandom(1, 100 - MatchWeight)
            }).ToList();

            informations = scored
                .OrderByDescending(a => a.Score)
                .Take(Utility.TrueRandom(2, 5))
                .Select(a => a.Information)
                .ToList();
        }

        var sb = new StringBuilder();
        foreach (var info in informations)
        {
            if (!string.IsNullOrWhiteSpace(info.Text))
                sb.AppendLine(info.Text).AppendLine();
        }

        return sb.ToString();
    }

    private async Task CreateInformationRequest(IEnumerable<string> tokens)
    {
        var information = new Information(null, tokens);
        await _dbContext.AddAsync(information);
        await _dbContext.SaveChangesAsync();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Database
    // ═════════════════════════════════════════════════════════════════════════

    private async Task SaveChatLog(
        ChatLog chatLog,
        string message,
        bool error,
        bool tokenLimitReached,
        DateTimeOffset receivedAt,
        long elapsedMilliseconds)
    {
        var chat = new Chat
        {
            History = chatLog.PrintHistory(),
            Message = chatLog.Message,
            Response = message,
            ReceivedAt = receivedAt,
            ResponseTookMs = elapsedMilliseconds,
            Error = error,
            TokenLimitReached = tokenLimitReached,
            SessionTrackingId = chatLog.UserTrackingId
        };

        await _dbContext.AddAsync(chat);
        await _dbContext.SaveChangesAsync();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Safely extracts a string value from a Bedrock tool-use input <see cref="Document"/>.
    /// Returns null if the key is missing or the value is not a string.
    /// </summary>
    private static string? GetStringArg(Document input, string key)
    {
        try
        {
            if (!input.IsDictionary()) return null;
            var dict = input.AsDictionary();
            if (dict.TryGetValue(key, out var val) && val.IsString())
                return val.AsString();
        }
        catch
        {
            // Silently absorb — defensive coding against SDK edge cases
        }

        return null;
    }
}