using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime.Documents;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos;
using PortfolioWebsite.Api.Services.Entities;
using PortfolioWebsite.Common;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PortfolioWebsite.Api.Services;

public class ChatService
{

    private record ToolExecutionResult(
        string ResultText,
        bool NeedsClarification = false,
        string? ClarificationQuestion = null,
        bool ReturnResume = false,
        string? RedirectToPage = null);

    private record RoutingResult(
        ConverseRequest? ConverseRequest,
        string? ShortCircuitResponse,
        bool ReturnResume,
        string? RedirectToPage,
        bool TokenLimitReached,
        DateTimeOffset ReceivedAt,
        Stopwatch Stopwatch);

    // Add these in ChatService or a shared models file
    public record StreamChunk
    {
        public string? Token { get; init; }
        public StreamMeta? Meta { get; init; }
        public bool IsToken => Token != null;
        public bool IsMeta => Meta != null;
    }

    public record StreamMeta(
        string? RedirectToPage = null,
        bool DisplayResume = false,
        bool TokenLimitReached = false);


    private const int MatchWeight = 70;
    private const int MaxContextEntries = 4;
    private const int MinTokenLength = 4;
    private const int MaxResponseTokens = 300;
    private const int MaxResponseWords = 200;

    private readonly ILogger<ChatService> _logger;
    private readonly SqlDbContext _dbContext;
    private readonly IDbContextFactory<SqlDbContext> _dbContextFactory;
    private readonly IAmazonBedrockRuntime _bedrockClient;
    private readonly ContactService _contactService;

    private record ModelSettings(string Model);
    private readonly ModelSettings _toolUse;
    private readonly ModelSettings _questions;

    private readonly ConcurrentDictionary<Guid, IReadOnlyList<string>> _keywordCache = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ChatService(
        IConfiguration configuration,
        ILogger<ChatService> logger,
        SqlDbContext dbContext,
        IDbContextFactory<SqlDbContext> dbContextFactory,
        IAmazonBedrockRuntime bedrockClient,
        ContactService contactService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _dbContextFactory = dbContextFactory; // for parallel reads
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

    public async IAsyncEnumerable<StreamChunk> StreamChat(
    ChatLog chat,
    [EnumeratorCancellation] CancellationToken ct)
    {
        var receivedAt = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();

        // Phase 1: Routing — capture errors without yielding in catch
        RoutingResult? routing = null;
        string? routingError = null;

        try
        {
            routing = await RunRoutingPhase(chat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during routing phase");
            routingError = "I'm sorry, I encountered an error. Please try again.";
        }

        if (routingError != null)
        {
            yield return new StreamChunk { Token = routingError };
            yield return new StreamChunk { Meta = new StreamMeta() };
            yield break;
        }

        // Short-circuit: clarification, error, resume, redirect
        if (routing!.ShortCircuitResponse != null)
        {
            yield return new StreamChunk { Token = routing.ShortCircuitResponse };
            yield return new StreamChunk
            {
                Meta = new StreamMeta(
                    RedirectToPage: routing.RedirectToPage,
                    DisplayResume: routing.ReturnResume,
                    TokenLimitReached: routing.TokenLimitReached)
            };

            stopwatch.Stop();
            await SaveChatLog(chat, routing.ShortCircuitResponse, false,
                routing.TokenLimitReached, receivedAt, stopwatch.ElapsedMilliseconds);
            yield break;
        }

        if (routing.ConverseRequest == null)
        {
            const string fallback = "I'm sorry, I had trouble forming a response. Please try again.";
            yield return new StreamChunk { Token = fallback };
            yield return new StreamChunk { Meta = new StreamMeta() };

            stopwatch.Stop();
            await SaveChatLog(chat, fallback, true, false, receivedAt, stopwatch.ElapsedMilliseconds);
            yield break;
        }

        // Phase 2: Stream Sonnet answer — capture error without yielding in catch
        ConverseStreamResponse? streamResponse = null;
        string? streamError = null;

        try
        {
            streamResponse = await _bedrockClient.ConverseStreamAsync(
                new ConverseStreamRequest
                {
                    ModelId = routing.ConverseRequest.ModelId,
                    System = routing.ConverseRequest.System,
                    Messages = routing.ConverseRequest.Messages,
                    InferenceConfig = routing.ConverseRequest.InferenceConfig
                }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting Bedrock stream");
            streamError = "I encountered an error. Please try again.";
        }

        if (streamError != null)
        {
            yield return new StreamChunk { Token = streamError };
            yield return new StreamChunk { Meta = new StreamMeta() };

            stopwatch.Stop();
            await SaveChatLog(chat, streamError, true, false, receivedAt, stopwatch.ElapsedMilliseconds);
            yield break;
        }

        var fullResponse = new StringBuilder();

        // No WithCancellation — CT is already passed to ConverseStreamAsync above
        foreach (var chunk in streamResponse!.Stream)
        {
            if (ct.IsCancellationRequested) break;

            if (chunk is ContentBlockDeltaEvent delta && delta.Delta?.Text != null)
            {
                fullResponse.Append(delta.Delta.Text);
                yield return new StreamChunk { Token = delta.Delta.Text };
            }
        }

        stopwatch.Stop();
        var completeResponse = fullResponse.ToString();

        yield return new StreamChunk
        {
            Meta = new StreamMeta(
                RedirectToPage: routing.RedirectToPage,
                DisplayResume: routing.ReturnResume,
                TokenLimitReached: routing.TokenLimitReached)
        };

        await SaveChatLog(chat, completeResponse, false,
            routing.TokenLimitReached, receivedAt, stopwatch.ElapsedMilliseconds);
    }


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

    public async Task<string?> GenerateHtmlResume(string? title, string? jobDescription)
    {
        var allInformation = await _dbContext.Information
            .OrderBy(t => t.Text)
            .ToListAsync();

        var allJobs = await _dbContext.WorkExperiences
            .Where(j => j.IsActive)
            .OrderBy(j => j.DisplayOrder)
            .ToListAsync();

        // Include WorkExperience so employer name is available for unlinked projects
        var allProjects = await _dbContext.Projects
            .Include(p => p.WorkExperience)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.IsFeatured)
            .ThenBy(p => p.WorkExperience!.DisplayOrder)
            .ThenBy(p => p.DisplayOrder)
            .ToListAsync();

        var infoBlock = string.Join(
            "\r\n\r\nNew Information:\r\n",
            allInformation.Select(i => i.Text));

        var jobsBlock = string.Join("\r\n\r\n", allJobs.Select(j =>
        {
            List<string> achievements;
            try { achievements = JsonSerializer.Deserialize<List<string>>(j.Achievements) ?? []; }
            catch { achievements = []; }

            var years = BuildYearRange(j.StartYear, j.EndYear);
            var sb = new StringBuilder();

            sb.AppendLine($"Role: {j.Title}");
            sb.AppendLine($"Employer: {j.Employer}{(years != null ? $" | {years}" : "")}");
            if (!string.IsNullOrWhiteSpace(j.Summary))
                sb.AppendLine($"Summary: {j.Summary}");
            if (achievements.Count > 0)
            {
                sb.AppendLine("Achievements:");
                foreach (var a in achievements)
                    sb.AppendLine($"  - {a}");
            }

            // Attach linked projects directly under their employer job entry
            var linkedProjects = allProjects
                .Where(p => p.WorkExperienceId == j.WorkExperienceId)
                .ToList();

            if (linkedProjects.Count > 0)
            {
                sb.AppendLine("  Related Projects:");
                foreach (var p in linkedProjects)
                {
                    var techStack = DeserializeTechStack(p.TechStack);
                    var projectYears = BuildYearRange(p.StartYear, p.EndYear);
                    sb.AppendLine($"    Project: {p.Title} | Role: {p.Role}{(projectYears != null ? $" | {projectYears}" : "")}");
                    if (!string.IsNullOrWhiteSpace(p.Summary)) sb.AppendLine($"      Summary: {p.Summary}");
                    if (!string.IsNullOrWhiteSpace(p.Detail)) sb.AppendLine($"      Detail: {p.Detail}");
                    if (!string.IsNullOrWhiteSpace(p.ImpactStatement)) sb.AppendLine($"      Impact: {p.ImpactStatement}");
                    if (techStack.Count > 0) sb.AppendLine($"      Tech Stack: {string.Join(", ", techStack)}");
                }
            }

            return sb.ToString().Trim();
        }));

        // Only include projects that are not linked to any job (unlinked orphans)
        var unlinkedProjects = allProjects.Where(p => p.WorkExperienceId is null).ToList();
        var unlinkedProjectsBlock = unlinkedProjects.Count > 0
            ? string.Join("\r\n\r\n", unlinkedProjects.Select(p =>
            {
                var techStack = DeserializeTechStack(p.TechStack);
                var years = BuildYearRange(p.StartYear, p.EndYear);
                var sb = new StringBuilder();

                sb.AppendLine($"Project: {p.Title}");
                sb.AppendLine($"Role: {p.Role}{(years != null ? $" | {years}" : "")}");
                if (!string.IsNullOrWhiteSpace(p.Summary)) sb.AppendLine($"Summary: {p.Summary}");
                if (!string.IsNullOrWhiteSpace(p.Detail)) sb.AppendLine($"Detail: {p.Detail}");
                if (!string.IsNullOrWhiteSpace(p.ImpactStatement)) sb.AppendLine($"Impact: {p.ImpactStatement}");
                if (techStack.Count > 0) sb.AppendLine($"Tech Stack: {string.Join(", ", techStack)}");

                return sb.ToString().Trim();
            }))
            : null;

        bool hasTailoring = !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(jobDescription);

        var titleInstruction = hasTailoring
            ? $"""
          Tailor this resume for the following:

          {(!string.IsNullOrWhiteSpace(title) ? $"Target Role: {title}" : "")}

          {(!string.IsNullOrWhiteSpace(jobDescription) ? $"""
          Job Description:
          {jobDescription}

          Instructions:
          - Mirror the language, keywords, and terminology used in the job description naturally throughout the resume
          - Identify the top 5-7 required or preferred skills from the job description and ensure they are visible
            and substantiated with real experience from Samuel's background — never fabricate
          - Prioritize and feature projects and experience most directly relevant to the job description
          - Lead every bullet point with the accomplishment or outcome most relevant to this role
          - If the job description mentions specific technologies, methodologies, or domains Samuel has
            experience with, make sure they appear prominently rather than buried
          - Write the professional summary specifically for this role, referencing the employer's priorities
            where you can infer them from the job description
          - De-emphasize or condense experience and projects with little relevance to this role
          """ : """
          - Prioritize projects and experience most relevant to this role title
          - Use language and keywords appropriate for this type of role
          - Write the professional summary targeting this role
          """)}
          """
            : "Include all experience and projects. Featured projects should appear more prominently.";

        var systemPrompt = $$"""
        You are a professional resume generator. I will provide raw information about Samuel Ohrenberg's 
        professional experience, education, and skills. Each job entry may include a list of related projects
        that were delivered during that role. Unlinked projects (not tied to a specific employer) will be
        provided separately at the end.

        Generate an HTML resume that meets these requirements:

        - Slots within a Vue.js <template></template> (do NOT include the <template> tags themselves)
        - Does NOT include a contact section
        - Uses ONLY inline style attributes — no <style> tags, no CSS class names
        - Includes ALL jobs, education, and important skills
        - For projects, you have flexibility in how you present them — choose whichever approach
          produces the strongest, most readable resume:
            Option A: Show each project as a sub-section directly under its employer job entry
            Option B: Collect all projects into a dedicated Projects section after Professional Experience
            Option C: A hybrid — inline the most impactful projects under their employer, and group
                      lesser projects in a standalone section
          Featured projects should always be prominent regardless of placement.
          Unlinked projects (no employer) should appear in a standalone Projects section.
        - Each project entry should show: title, role, year range, a concise summary or bullet
          points from the detail, the impact statement as a highlighted callout if present, and
          the tech stack as small inline tags
        - Is visually appealing, accessible (screen-reader friendly), and responsive
        - Has strong contrast between foreground text and background colors
        - Paraphrases and summarizes as needed while retaining all important details

        Use this color theme throughout. These are NON-NEGOTIABLE — do not deviate:
            Page background:         #e6eeee  (light)
            Card/section background: #FFFFFF
            ALL body text:           #1a1a1a  (near-black — must be readable on light backgrounds)
            ALL headings:            #1a1a1a  (near-black)
            Accent / section titles: #006a6a
            Highlighted text / links:#48A9A6
            Impact callout background:#e0f4f4
            Impact callout text:     #004d4d
            Tech tag background:     #d0ecec
            Tech tag text:           #006a6a
            Subtle dividers:         #c0d8d8

        CRITICAL: This resume will always render on a LIGHT background.
        Every text element must have dark text (#1a1a1a or similar) to ensure readability.
        Never use white, near-white, or light-colored text anywhere.
        Never use the site's dark theme colors (#001e1e, #003131, rgba with low opacity) for text.

        {{titleInstruction}}

        You MUST output ONLY a valid JSON object in exactly this format with no markdown, no code fences, and no extra text:
        {"html": "<your complete html string here>"}
        """;

        var userContent = new StringBuilder();
        userContent.AppendLine("=== BIOGRAPHICAL & SKILLS INFORMATION ===");
        userContent.AppendLine(infoBlock);
        userContent.AppendLine();
        userContent.AppendLine("=== PROFESSIONAL EXPERIENCE (projects linked where applicable) ===");
        userContent.AppendLine(jobsBlock);

        if (unlinkedProjectsBlock is not null)
        {
            userContent.AppendLine();
            userContent.AppendLine("=== ADDITIONAL PROJECTS (not tied to a specific employer) ===");
            userContent.AppendLine(unlinkedProjectsBlock);
        }

        var request = new ConverseRequest
        {
            ModelId = _questions.Model,
            System = [new SystemContentBlock { Text = systemPrompt }],
            Messages =
            [
                new Message
                {
                    Role = "user",
                    Content = [new ContentBlock { Text = userContent.ToString() }]
                }
            ],
            InferenceConfig = new InferenceConfiguration { MaxTokens = 8192 }
        };

        var result = await _bedrockClient.ConverseAsync(request);
        var raw = result.Output.Message.Content
            .FirstOrDefault(c => c.Text != null)?.Text ?? string.Empty;

        raw = raw.Trim();

        if (raw.StartsWith("```"))
        {
            raw = string.Join('\n', raw.Split('\n').Skip(1));
            if (raw.TrimEnd().EndsWith("```"))
                raw = raw[..raw.LastIndexOf("```")].TrimEnd();
            raw = raw.Trim();
        }

        try
        {
            var node = JsonNode.Parse(raw, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
            var html = node?["html"]?.ToString();
            if (!string.IsNullOrWhiteSpace(html))
                return html;
        }
        catch { }

        var htmlStart = raw.IndexOf('<');
        if (htmlStart > 0)
        {
            var extracted = raw[htmlStart..].Trim();
            if (extracted.EndsWith("}\"") || extracted.EndsWith("\"}"))
                extracted = extracted[..extracted.LastIndexOf('<')].Trim();
            return extracted;
        }

        _logger.LogWarning("Could not extract HTML from resume response");
        return raw;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Bedrock Core: Tool-Use Routing
    // ═════════════════════════════════════════════════════════════════════════


    private async Task<RoutingResult> RunRoutingPhase(ChatLog chat)
    {
        var receivedAt = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();

        var (converseRequest, shortCircuitResponse, returnResume, redirectToPage, tokenLimitReached) =
            await ResolveRoutingToRequest(chat);

        stopwatch.Stop();

        return new RoutingResult(
            ConverseRequest: converseRequest,
            ShortCircuitResponse: shortCircuitResponse,
            ReturnResume: returnResume,
            RedirectToPage: redirectToPage,
            TokenLimitReached: tokenLimitReached,
            ReceivedAt: receivedAt,
            Stopwatch: stopwatch);
    }

    private async Task<(
        ConverseRequest? ConverseRequest,
        string? ShortCircuitResponse,
        bool ReturnResume,
        string? RedirectToPage,
        bool TokenLimitReached)>
        ResolveRoutingToRequest(ChatLog chat)
    {
        // Same system prompt as QueryBedrock
        const string systemPrompt = """
            You are SamuelLM, an AI chatbot created by Samuel Ohrenberg (also known as Sam or Sammy).
            Your website is https://aboutsamuel.com/.
            You answer on behalf of Samuel — respond as if you were him in a professional interview setting.
            You are professional, friendly, and helpful.
            Do not output markdown. Use plain text only.

            For greetings, small talk, or messages that are not questions, respond warmly WITHOUT calling any tool.

            Only call a tool when the user's message clearly warrants one:
            - askQuestion: for specific questions about Samuel's background, skills, experience, or projects
            - askClarification: when the user's request is genuinely ambiguous
            - contactSamuel: when the user wants to get in touch with Sam
            - getResume: when the user wants to see or download Sam's resume
            - redirectToPage: when the user wants content best found on a specific page

            ════════════════════════════════════════════
            ABSOLUTE RULES — never violate these:
            1. NEVER write tool names in your text response under any circumstances.
               Never output text like "askQuestion: ..." or "contactSamuel: ..." or any tool name as text.
            2. NEVER describe what you are about to do. Never say "I'll look that up" or "Let me check".
            3. When calling a tool, your text content must be completely empty — nothing at all.
            4. Tool calls happen silently via the tool_use mechanism only, never via text.
            ════════════════════════════════════════════
            """;

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

        const int MaxIterations = 6;
        int iteration = 0;
        bool returnResume = false;
        bool tokenLimitReached = false;
        string? redirectToPage = null;
        ConverseRequest? preparedQuestionRequest = null;

        while (iteration++ < MaxIterations)
        {
            ConverseResponse result;
            try
            {
                result = await _bedrockClient.ConverseAsync(new ConverseRequest
                {
                    ModelId = _toolUse.Model,
                    System = [new SystemContentBlock { Text = systemPrompt }],
                    Messages = messages,
                    ToolConfig = new ToolConfiguration
                    {
                        Tools = GetToolDefinitions(),
                        ToolChoice = new ToolChoice { Auto = new AutoToolChoice() }
                    },
                    InferenceConfig = new InferenceConfiguration { MaxTokens = 1000 }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in routing loop (iteration {Iteration})", iteration);
                return (null, "I'm sorry, I encountered an error. Please try again.", false, null, false);
            }

            tokenLimitReached = tokenLimitReached || (result.Usage?.TotalTokens ?? 0) > 2500;
            messages.Add(result.Output.Message);

            // Model is done — either inline text response or we have a prepared question request
            if (result.StopReason == "end_turn")
            {
                if (preparedQuestionRequest != null)
                    return (preparedQuestionRequest, null, returnResume, redirectToPage, tokenLimitReached);

                var inlineText = result.Output.Message.Content
                    .FirstOrDefault(c => c.Text != null)?.Text?.Trim();

                // Safety net: detect leaked tool narration and recover
                if (IsLeakedToolCall(inlineText))
                {
                    _logger.LogWarning("Detected leaked tool narration: {Text}", inlineText);

                    // Extract whatever question-like content we can and run it properly
                    var fallbackQuestion = ExtractQuestionFromLeak(inlineText, chat.Message);
                    var (request, shortCircuit, shortCircuitResponse) =
                        await BuildQuestionRequest(messages, systemPrompt, fallbackQuestion);

                    if (shortCircuit)
                        return (null, shortCircuitResponse!.Message, returnResume, redirectToPage, tokenLimitReached);

                    return (request, null, returnResume, redirectToPage, tokenLimitReached);
                }

                return (null, inlineText, returnResume, redirectToPage, tokenLimitReached);
            }

            if (result.StopReason == "tool_use")
            {
                var toolResultBlocks = new List<ContentBlock>();

                foreach (var block in result.Output.Message.Content.Where(b => b.ToolUse != null))
                {
                    var toolUse = block.ToolUse;
                    _logger.LogInformation("Routing tool: {ToolName} (iteration {Iteration})",
                        toolUse.Name, iteration);

                    string toolResultText;

                    switch (toolUse.Name)
                    {
                        case "askQuestion":
                            {
                                // Build the Sonnet request but don't execute it yet — we'll stream it
                                var question = GetStringArg(toolUse.Input, "question");
                                var (request, shortCircuit, shortCircuitResponse) =
                                    await BuildQuestionRequest(messages, systemPrompt, question);

                                if (shortCircuit)
                                    return (null, shortCircuitResponse!.Message, returnResume, redirectToPage, tokenLimitReached);

                                preparedQuestionRequest = request;

                                // Feed a placeholder back to Haiku so it can reach end_turn cleanly
                                toolResultText = "Answer prepared for streaming to user.";
                                break;
                            }

                        case "askClarification":
                            {
                                var question = GetStringArg(toolUse.Input, "question")
                                    ?? "Could you tell me a bit more about what you're looking for?";
                                // Return immediately — clarification goes straight to user
                                return (null, question, returnResume, redirectToPage, tokenLimitReached);
                            }

                        case "contactSamuel":
                            {
                                var email = GetStringArg(toolUse.Input, "email");
                                var msg = GetStringArg(toolUse.Input, "message");
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
                                    contactError = "A system error occurred.";
                                }

                                var confirmation = await GetContactConfirmationMessage(email, msg, contactError);
                                // Contact confirmation is short — always short-circuit, no streaming needed
                                return (null, confirmation, false, null, tokenLimitReached);
                            }

                        case "getResume":
                            returnResume = true;
                            toolResultText = "Resume retrieved successfully. Inform the user their resume is ready.";
                            break;

                        case "redirectToPage":
                            redirectToPage = GetStringArg(toolUse.Input, "page");
                            toolResultText = $"User is being redirected to the {redirectToPage} page.";
                            break;

                        default:
                            _logger.LogWarning("Unrecognized tool: {ToolName}", toolUse.Name);
                            toolResultText = $"Unknown tool '{toolUse.Name}'.";
                            break;
                    }

                    toolResultBlocks.Add(new ContentBlock
                    {
                        ToolResult = new ToolResultBlock
                        {
                            ToolUseId = toolUse.ToolUseId,
                            Content = [new ToolResultContentBlock { Text = toolResultText }]
                        }
                    });
                }

                messages.Add(new Message { Role = "user", Content = toolResultBlocks });
                continue;
            }

            _logger.LogWarning("Unexpected stop reason: {StopReason}", result.StopReason);
            break;
        }

        return (null, "I'm sorry, I had trouble forming a response. Please try again.", false, null, tokenLimitReached);
    }

    private static readonly string[] _toolNames =
    ["askQuestion", "askClarification", "contactSamuel", "getResume", "redirectToPage"];

    private static bool IsLeakedToolCall(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return _toolNames.Any(tool =>
            text.StartsWith(tool + ":", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith(tool + " ", StringComparison.OrdinalIgnoreCase) ||
            text.Contains($"\"{tool}\"", StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractQuestionFromLeak(string leakedText, string originalMessage)
    {
        // Try to pull the parameter value out of something like:
        // "askQuestion: what does Sam know about ASP.NET"
        // "askQuestion: {"question": "ASP.NET experience"}"
        foreach (var tool in _toolNames)
        {
            var prefix = tool + ":";
            if (leakedText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = leakedText[prefix.Length..].Trim();
                // Strip any JSON wrapping if present
                if (remainder.StartsWith("{"))
                {
                    try
                    {
                        var doc = JsonDocument.Parse(remainder);
                        if (doc.RootElement.TryGetProperty("question", out var q))
                            return q.GetString() ?? originalMessage;
                    }
                    catch { }
                }
                // Plain text parameter — use it directly if it's meaningful
                if (remainder.Length > 5)
                    return remainder;
            }
        }

        // Couldn't extract anything useful — fall back to the original user message
        return originalMessage;
    }

    private async Task<ChatResponse> QueryBedrock(ChatLog chat)
    {
        const string systemPrompt = """
        You are SamuelLM, an AI chatbot created by Samuel Ohrenberg (also known as Sam or Sammy).
        Your website is https://aboutsamuel.com/.
        You are hosted on Sam's infrastructure as an AI assistant for his portfolio website,
        built with an ASP.NET Core Web API and a Vue.js front end.
        You answer on behalf of Samuel — respond as if you were him in a professional interview setting.
        You are professional, friendly, and helpful.
        Do not output markdown. Use plain text only.

        For greetings, small talk, or messages that are not questions (e.g. "hello", "hello world",
        "how are you", "thanks"), respond warmly and conversationally WITHOUT calling any tool.
        Briefly introduce yourself and invite the user to ask a question about Samuel.

        Only call a tool when the user's message clearly warrants one:
        - askQuestion: for specific questions about Samuel's background, skills, experience, or projects
        - askClarification: when the user's request is vague and a clarifying question would
          meaningfully improve the answer. Use sparingly — only when genuinely ambiguous.
        - contactSamuel: when the user wants to get in touch with Sam
        - getResume: when the user wants to see or download Sam's resume
        - redirectToPage: when the user wants content best found on a specific page

        You may call multiple tools in a single turn if needed (e.g. askQuestion AND redirectToPage).
        
        Tool results will be returned to you. Use them to construct your final answer.
        When you have enough information, respond directly to the user with end_turn.

        IMPORTANT: Never describe, mention, or reference tool calls in your response.
        Never say things like "I'll call askQuestion" or "I'm looking that up".
        Your final response to the user should be the answer only — never the process.
        """;

        // Build the initial message list from chat history + new message
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

        const int MaxIterations = 6;
        int iteration = 0;
        bool returnResume = false;
        bool error = false;
        bool tokenLimitReached = false;
        string? redirectToPage = null;

        while (iteration++ < MaxIterations)
        {
            ConverseResponse result;
            try
            {
                result = await _bedrockClient.ConverseAsync(new ConverseRequest
                {
                    ModelId = _toolUse.Model,
                    System = [new SystemContentBlock { Text = systemPrompt }],
                    Messages = messages,
                    ToolConfig = new ToolConfiguration
                    {
                        Tools = GetToolDefinitions(),
                        ToolChoice = new ToolChoice { Auto = new AutoToolChoice() }
                    },
                    InferenceConfig = new InferenceConfiguration { MaxTokens = 1000 }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Bedrock Converse API (iteration {Iteration})", iteration);
                return new ChatResponse("I'm sorry, I encountered an error. Please try again.", true);
            }

            tokenLimitReached = tokenLimitReached || (result.Usage?.TotalTokens ?? 0) > 2500;

            // Append the assistant's response to the message history
            // This is required — Bedrock needs to see its own previous turns
            messages.Add(result.Output.Message);

            // Model is done — extract the final text response
            if (result.StopReason == "end_turn")
            {
                var text = result.Output.Message.Content
                    .FirstOrDefault(c => c.Text != null)?.Text ?? string.Empty;

                if (string.IsNullOrWhiteSpace(text))
                    text = "I'm sorry, I wasn't sure how to handle that. Could you rephrase your question?";

                return new ChatResponse(text, error, tokenLimitReached, returnResume, redirectToPage);
            }

            // Model wants to use tools
            if (result.StopReason == "tool_use")
            {
                var toolResultBlocks = new List<ContentBlock>();

                foreach (var block in result.Output.Message.Content.Where(b => b.ToolUse != null))
                {
                    var toolUse = block.ToolUse;
                    _logger.LogInformation("Tool selected: {ToolName} (iteration {Iteration})",
                        toolUse.Name, iteration);

                    var toolResult = await ExecuteTool(toolUse.Name, toolUse.Input, messages, systemPrompt);

                    // Clarification is special — we stop the loop and return immediately to the user
                    if (toolResult.NeedsClarification)
                        return new ChatResponse(toolResult.ClarificationQuestion!, false, tokenLimitReached);

                    // Accumulate side effects across multiple tool calls in one turn
                    if (toolResult.ReturnResume) returnResume = true;
                    if (toolResult.RedirectToPage != null) redirectToPage = toolResult.RedirectToPage;
                    error = error || toolResult.ResultText.StartsWith("ERROR:");

                    // Each tool result gets added as a block — Bedrock requires one ToolResult
                    // per ToolUse, matched by ToolUseId
                    toolResultBlocks.Add(new ContentBlock
                    {
                        ToolResult = new ToolResultBlock
                        {
                            ToolUseId = toolUse.ToolUseId,
                            Content = [new ToolResultContentBlock { Text = toolResult.ResultText }]
                        }
                    });
                }

                // Feed all tool results back to the model as a single user turn
                messages.Add(new Message
                {
                    Role = "user",
                    Content = toolResultBlocks
                });

                // Loop continues — model will process results and either answer or call more tools
                continue;
            }

            // Unexpected stop reason
            _logger.LogWarning("Unexpected stop reason: {StopReason}", result.StopReason);
            break;
        }

        return new ChatResponse(
            "I'm sorry, I had trouble forming a response. Please try again.",
            true, tokenLimitReached, returnResume, redirectToPage);
    }

    private async Task<ToolExecutionResult> ExecuteTool(
    string toolName,
    Document toolInput,
    List<Message> conversationHistory,
    string systemPrompt)
    {
        switch (toolName)
        {
            case "askQuestion":
                {
                    var question = GetStringArg(toolInput, "question");
                    var response = await AskBedrockQuestion(conversationHistory, systemPrompt, question);
                    return new ToolExecutionResult(response.Message);
                }

            case "askClarification":
                {
                    var question = GetStringArg(toolInput, "question");
                    if (string.IsNullOrWhiteSpace(question))
                        return new ToolExecutionResult("Could you tell me a bit more about what you're looking for?");

                    // Signal the loop to stop and return this question to the user
                    return new ToolExecutionResult(
                        ResultText: question,
                        NeedsClarification: true,
                        ClarificationQuestion: question);
                }

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
                        contactError = "A system error occurred while sending the request.";
                    }

                    // We generate the confirmation message via Bedrock so it sounds natural
                    // but this result text goes back to the routing model to formulate its reply
                    var confirmation = await GetContactConfirmationMessage(email, msg, contactError);
                    return new ToolExecutionResult(
                        contactError != null ? $"ERROR: {contactError}" : confirmation);
                }

            case "getResume":
                return new ToolExecutionResult(
                    ResultText: "Resume retrieved successfully. Inform the user their resume is ready.",
                    ReturnResume: true);

            case "redirectToPage":
                {
                    var page = GetStringArg(toolInput, "page");
                    return new ToolExecutionResult(
                        ResultText: $"User is being redirected to the {page} page.",
                        RedirectToPage: page);
                }

            default:
                _logger.LogWarning("Unrecognized tool: {ToolName}", toolName);
                return new ToolExecutionResult($"Unknown tool '{toolName}' was called.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Bedrock: Q&A with RAG
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<ChatResponse> AskBedrockQuestion(
        List<Message> conversationHistory,
        string baseSystemPrompt,
        string? explicitQuestion)
    {
        var (request, shortCircuit, shortCircuitResponse) =
            await BuildQuestionRequest(conversationHistory, baseSystemPrompt, explicitQuestion);

        if (shortCircuit)
            return shortCircuitResponse!;

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

    private async Task<(ConverseRequest Request, bool ShouldShortCircuit, ChatResponse? ShortCircuitResponse)>
        BuildQuestionRequest(
            List<Message> conversationHistory,
            string baseSystemPrompt,
            string? explicitQuestion)
    {
        var queryText = !string.IsNullOrWhiteSpace(explicitQuestion)
            ? explicitQuestion
            : BuildUserQueryText(conversationHistory);

        if (queryText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3)
        {
            return (null!, true, new ChatResponse(
                "Hi there! I'm SamuelLM, Sam's portfolio assistant. Feel free to ask me anything about his background, skills, or experience!",
                false));
        }

        var tokens = Tokenizer.Tokenize(queryText).ToList();
        var relevantInfo = await GetRelevantInformation(tokens);

        if (string.IsNullOrWhiteSpace(relevantInfo))
        {
            await CreateInformationRequest(tokens);
            return (null!, true, new ChatResponse(
                "I'm sorry, I don't have information about that topic yet. " +
                "I've made a note of the gap and will work to get it added!",
                false));
        }

        var enrichedSystemPrompt = $"""
        {baseSystemPrompt}

        ════════════════════════════════════════════
        CONTEXT — use ONLY the information below to answer.
        Do NOT use your training data. Do NOT invent projects,
        employers, technologies, dates, or outcomes.
        If the answer is not in the context, say so honestly.
        ════════════════════════════════════════════

        {relevantInfo}

        ════════════════════════════════════════════
        RESPONSE RULES — follow all of these:
        - Maximum {MaxResponseWords} words. Be concise.
        - Plain text only — no markdown, no bullet points, no lists
        - Speak as Samuel in first person, professional but brief
        - If context is insufficient, say: "I don't have detailed 
          information about that, but feel free to ask me something else."
        - Never add closing offers like "Would you like to know more?" 
          or "Feel free to ask!" — just answer and stop
        ════════════════════════════════════════════
        """;

        var cleanHistory = BuildCleanHistory(conversationHistory);

        var request = new ConverseRequest
        {
            ModelId = _questions.Model,
            System = [new SystemContentBlock { Text = enrichedSystemPrompt }],
            Messages = cleanHistory,
            InferenceConfig = new InferenceConfiguration { MaxTokens = MaxResponseTokens }
        };

        return (request, false, null);
    }


    /// <summary>
    /// Strips ToolUse and ToolResult content blocks from message history,
    /// returning only plain text turns. This prevents the Q&A model from
    /// seeing agentic loop internals it has no context to interpret.
    /// </summary>
    private static List<Message> BuildCleanHistory(List<Message> messages)
    {
        var clean = new List<Message>();

        foreach (var message in messages)
        {
            // Keep only content blocks that are plain text
            var textBlocks = message.Content
                .Where(c => c.Text != null && c.ToolUse == null && c.ToolResult == null)
                .ToList();

            // Skip messages that are entirely tool-related (no text content at all)
            if (textBlocks.Count == 0)
                continue;

            clean.Add(new Message
            {
                Role = message.Role,
                Content = textBlocks
            });
        }

        return clean;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Bedrock: Contact Confirmation Message
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
    // Tool Definitions
    // ═════════════════════════════════════════════════════════════════════════

    private static List<Tool> GetToolDefinitions()
    {
        return
        [
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

            new Tool
            {
                ToolSpec = new ToolSpecification
                {
                    Name = "redirectToPage",
                    Description =
                        "Redirects the user to a relevant page on the website when their query is best " +
                        "answered by navigating them there directly. Available pages:\n" +
                        "  - 'Contact':     A form the user can fill out to reach out to Samuel.\n" +
                        "  - 'Resume':      Samuel's full work history, projects, education, and skills.\n" +
                        "  - 'Projects':    Samuel's featured projects with tech stack details.\n" +
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
                                        "The page to redirect the user to. Must be exactly one of: 'Contact', 'Projects', 'Resume'.")
                                })
                            }),
                            ["required"] = new Document(new List<Document> { new Document("page") })
                        })
                    }
                }
            },

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
                        Json = new Document(new Dictionary<string, Document>
                        {
                            ["type"]       = new Document("object"),
                            ["properties"] = new Document(new Dictionary<string, Document>())
                        })
                    }
                }
            },

            new Tool
            {
                ToolSpec = new ToolSpecification
                {
                    Name = "askClarification",
                    Description =
                        "Ask the user a clarifying question when their request is genuinely ambiguous " +
                        "and a better answer depends on knowing more. For example: 'tell me about his projects' " +
                        "could mean frontend projects, backend, all of them, or a specific one. " +
                        "Do NOT use this for clear questions. Use it sparingly.",
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
                                        "The clarifying question to ask the user. Should be short, " +
                                        "friendly, and specific about what additional info would help.")
                                })
                            }),
                            ["required"] = new Document(new List<Document> { new Document("question") })
                        })
                    }
                }
            },

            new Tool
            {
                ToolSpec = new ToolSpecification
                {
                    Name = "askQuestion",
                    Description =
                        "Answers specific questions about Samuel Ohrenberg — his technical skills, " +
                        "work experience, projects, education, and professional background. " +
                        "Do NOT use this for greetings, small talk, or messages that are not questions " +
                        "about Samuel. For those, respond directly without using any tool.",
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
        var tokenList = tokens.Where(t => t.Length >= MinTokenLength).ToList();

        // Create three independent contexts and fire all queries simultaneously
        await using var ctx1 = await _dbContextFactory.CreateDbContextAsync();
        await using var ctx2 = await _dbContextFactory.CreateDbContextAsync();
        await using var ctx3 = await _dbContextFactory.CreateDbContextAsync();

        var informationTask = ctx1.Information
            .Include(i => i.Keywords)
            .ToListAsync();

        var projectsTask = ctx2.Projects
            .Include(p => p.WorkExperience)
            .Where(p => p.IsActive)
            .ToListAsync();

        var workTask = ctx3.WorkExperiences
            .Where(j => j.IsActive)
            .ToListAsync();

        await Task.WhenAll(informationTask, projectsTask, workTask);

        var informations = await informationTask;
        var projects = await projectsTask;
        var work = await workTask;

        // Everything below this line is unchanged
        var projectInfos = projects.Select(BuildProjectInformation).ToList();
        var workInfos = work.Select(BuildWorkExperienceInformation).ToList();
        var allEntries = informations.Concat(projectInfos).Concat(workInfos).ToList();

        foreach (var info in allEntries)
        {
            var cached = _keywordCache.GetOrAdd(
                info.InformationId,
                _ => Tokenizer.Tokenize(info.Text).ToList());

            var newKeywords = cached
                .Where(t => info.Keywords.All(k => k.Text != t))
                .Select(t => new Keyword(t, info))
                .ToList();

            info.Keywords.AddRange(newKeywords);
        }

        if (allEntries.Count <= MaxContextEntries)
            return BuildContextBlock(allEntries);

        var scored = allEntries.Select(i => new
        {
            Information = i,
            Score = ScoreEntry(i, tokenList)
        }).ToList();

        var top = scored
            .Where(s => s.Score > 0)
            .OrderByDescending(s => s.Score)
            .Take(MaxContextEntries)
            .Select(s => s.Information)
            .ToList();

        if (top.Count == 0)
            top = scored
                .OrderByDescending(s => s.Score)
                .Take(MaxContextEntries)
                .Select(s => s.Information)
                .ToList();

        return BuildContextBlock(top);
    }

    private static float ScoreEntry(Information entry, IReadOnlyList<string> tokens)
    {
        int matches = entry.Keywords.Count(k =>
        {
            var kText = k.Text.ToLower();

            if (tokens.Any(t => t == kText))
                return true;

            if (kText.Length < MinTokenLength)
                return false;

            return tokens.Any(t =>
                t.Length >= MinTokenLength &&
                Utility.LevenshteinDifference(kText, t) <= 25);
        });

        if (matches == 0) return 0;

        float density = (float)matches / Math.Max(entry.Keywords.Count, 1);
        float jitter = Utility.TrueRandom(1, 10);

        return (density * MatchWeight) + jitter;
    }

    private static Information BuildProjectInformation(Project project)
    {
        var techStack = DeserializeTechStack(project.TechStack);
        var years = BuildYearRange(project.StartYear, project.EndYear);
        var employer = project.WorkExperience?.Employer;

        var sb = new StringBuilder();
        sb.AppendLine($"Project: {project.Title}");
        if (employer is not null) sb.AppendLine($"Employer: {employer}");
        sb.AppendLine($"Role: {project.Role}");
        if (years != null) sb.AppendLine($"Years: {years}");
        sb.AppendLine($"Summary: {project.Summary}");
        if (!string.IsNullOrWhiteSpace(project.Detail))
            sb.AppendLine($"Detail: {project.Detail}");
        if (!string.IsNullOrWhiteSpace(project.ImpactStatement))
            sb.AppendLine($"Impact: {project.ImpactStatement}");
        if (techStack.Count > 0)
            sb.AppendLine($"Tech Stack: {string.Join(", ", techStack)}");

        var keywords = techStack
            .Select(t => new Keyword(t.ToLower(), null!))
            .Concat(Tokenizer.Tokenize(
                $"{project.Title} {employer} {project.Role} {project.Summary} {project.Detail} {project.ImpactStatement}")
                .Select(t => new Keyword(t, null!)))
            .ToList();

        return new Information(project.ProjectId, sb.ToString().Trim(), keywords);
    }

    private static Information BuildWorkExperienceInformation(WorkExperience job)
    {
        List<string> achievements;
        try { achievements = JsonSerializer.Deserialize<List<string>>(job.Achievements) ?? []; }
        catch { achievements = []; }

        var years = BuildYearRange(job.StartYear, job.EndYear);

        var sb = new StringBuilder();
        sb.AppendLine($"Job: {job.Title}");
        sb.AppendLine($"Employer: {job.Employer}");
        if (years != null) sb.AppendLine($"Years: {years}");
        if (!string.IsNullOrWhiteSpace(job.Summary))
            sb.AppendLine($"Summary: {job.Summary}");
        if (achievements.Count > 0)
        {
            sb.AppendLine("Achievements:");
            foreach (var achievement in achievements)
                sb.AppendLine($"  - {achievement}");
        }

        var keywords = Tokenizer
            .Tokenize($"{job.Title} {job.Employer} {job.Summary} {string.Join(' ', achievements)}")
            .Select(t => new Keyword(t, null!))
            .ToList();

        return new Information(job.WorkExperienceId, sb.ToString().Trim(), keywords);
    }

    private static string BuildContextBlock(IEnumerable<Information> entries)
    {
        var sb = new StringBuilder();
        foreach (var info in entries)
        {
            if (!string.IsNullOrWhiteSpace(info.Text))
                sb.AppendLine(info.Text).AppendLine();
        }
        return sb.ToString().Trim();
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

    private static string BuildUserQueryText(IEnumerable<Message> history)
    {
        var sb = new StringBuilder();
        foreach (var msg in history.Where(m => m.Role == "user"))
        {
            foreach (var block in msg.Content.Where(c => c.Text != null))
                sb.Append(block.Text).Append(' ');
        }
        return sb.ToString();
    }

    private static List<string> DeserializeTechStack(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    private static string? BuildYearRange(string? start, string? end)
    {
        if (start == null) return null;
        return end != null ? $"{start}–{end}" : $"{start}–Present";
    }

    private static string? GetStringArg(Document input, string key)
    {
        try
        {
            if (!input.IsDictionary()) return null;
            var dict = input.AsDictionary();
            if (dict.TryGetValue(key, out var val) && val.IsString())
                return val.AsString();
        }
        catch { }

        return null;
    }
}