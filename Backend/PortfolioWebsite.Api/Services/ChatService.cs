using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
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
        string? SystemPrompt,
        List<LlmMessage>? Messages,
        LlmToolDefinition[]? Tools,
        string? ShortCircuitResponse,
        bool ReturnResume,
        string? RedirectToPage,
        bool TokenLimitReached,
        DateTimeOffset ReceivedAt,
        Stopwatch Stopwatch);

    public record StreamChunk
    {
        public string? Token { get; init; }
        public StreamMeta? Meta { get; init; }
        public bool IsToken => Token != null;
        public bool IsMeta => Meta != null;
    }

    public record ResumeData(
        string Name,
        string Title,
        string Summary,
        List<string> CoreSkills,
        List<ResumeJob> Experience,
        List<ResumeProject> AdditionalProjects);

    public record ResumeJob(
        string Employer,
        string Title,
        string? Years,
        string Summary,
        List<string> Achievements,
        List<ResumeProject> Projects);

    public record ResumeProject(
        string Title,
        string Role,
        string? Years,
        string Summary,
        string? Impact,
        List<string> TechStack,
        bool IsFeatured);

    public record StreamMeta(
        string? RedirectToPage = null,
        bool DisplayResume = false,
        bool TokenLimitReached = false);


    private const int MaxContextEntries = 8;
    private const int MinTokenLength = 4;
    private const int MaxResponseTokens = 500;
    private const int MaxResponseWords = 250;


    private readonly ILogger<ChatService> _logger;
    private readonly SqlDbContext _dbContext;
    private readonly IDbContextFactory<SqlDbContext> _dbContextFactory;
    private readonly ILlmService _llm;
    private readonly IEmbeddingService _embeddings;
    private readonly ContactService _contactService;

    private readonly ConcurrentDictionary<Guid, IReadOnlyList<string>> _keywordCache = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ChatService(
        ILogger<ChatService> logger,
        SqlDbContext dbContext,
        IDbContextFactory<SqlDbContext> dbContextFactory,
        ILlmService llm,
        IEmbeddingService embeddings,
        ContactService contactService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _dbContextFactory = dbContextFactory;
        _llm = llm;
        _embeddings = embeddings;
        _contactService = contactService;
    }


    public async IAsyncEnumerable<StreamChunk> StreamChat(
        ChatLog chat,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var receivedAt = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();

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

        var fullResponse = new StringBuilder();

        string? streamError = null;
        IAsyncEnumerable<string> tokenStream = _llm.StreamAsync(
            routing.SystemPrompt!,
            routing.Messages!,
            MaxResponseTokens,
            ct);

        await foreach (var token in tokenStream.WithCancellation(ct))
        {
            fullResponse.Append(token);
            yield return new StreamChunk { Token = token };
        }

        if (streamError != null)
        {
            yield return new StreamChunk { Token = streamError };
            yield return new StreamChunk { Meta = new StreamMeta() };
            stopwatch.Stop();
            await SaveChatLog(chat, streamError, true, false, receivedAt, stopwatch.ElapsedMilliseconds);
            yield break;
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

        var routing = await RunRoutingPhase(chat);

        string response;
        bool error = false;

        if (routing.ShortCircuitResponse != null)
        {
            response = routing.ShortCircuitResponse;
        }
        else
        {
            try
            {
                response = await _llm.CompleteAsync(
                    routing.SystemPrompt!,
                    routing.Messages!,
                    MaxResponseTokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing chat response");
                response = "I'm sorry, I encountered an error. Please try again.";
                error = true;
            }
        }

        stopwatch.Stop();
        await SaveChatLog(chat, response, error, routing.TokenLimitReached,
            receivedAt, stopwatch.ElapsedMilliseconds);

        return new ChatResponse(response, error, routing.TokenLimitReached,
            routing.ReturnResume, routing.RedirectToPage);
    }

    public async Task<ResumeData?> GenerateResumeData(string? title, string? jobDescription)
    {
        var allInformation = await _dbContext.Information.OrderBy(t => t.Text).ToListAsync();
        var allJobs = await _dbContext.WorkExperiences
            .Where(j => j.IsActive).OrderBy(j => j.DisplayOrder).ToListAsync();
        var allProjects = await _dbContext.Projects
            .Include(p => p.WorkExperiences)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.IsFeatured)
            .ThenBy(p => p.WorkExperiences.Any()
                ? p.WorkExperiences.Min(w => w.DisplayOrder) : int.MaxValue)
            .ThenBy(p => p.DisplayOrder)
            .ToListAsync();

        var infoBlock = string.Join("\r\n\r\nNew Information:\r\n",
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
            if (!string.IsNullOrWhiteSpace(j.Summary)) sb.AppendLine($"Summary: {j.Summary}");
            if (achievements.Count > 0)
            {
                sb.AppendLine("Achievements:");
                foreach (var a in achievements) sb.AppendLine($"  - {a}");
            }

            var linked = allProjects
                .Where(p => p.WorkExperiences.Any(w => w.WorkExperienceId == j.WorkExperienceId))
                .ToList();

            if (linked.Count > 0)
            {
                sb.AppendLine("Related Projects:");
                foreach (var p in linked)
                {
                    var tech = DeserializeTechStack(p.TechStack);
                    sb.AppendLine($"  Project: {p.Title} | Role: {p.Role} | Featured: {p.IsFeatured}");
                    if (!string.IsNullOrWhiteSpace(p.Summary)) sb.AppendLine($"    Summary: {p.Summary}");
                    if (!string.IsNullOrWhiteSpace(p.ImpactStatement)) sb.AppendLine($"    Impact: {p.ImpactStatement}");
                    if (tech.Count > 0) sb.AppendLine($"    Tech: {string.Join(", ", tech)}");
                }
            }

            return sb.ToString().Trim();
        }));

        var unlinked = allProjects.Where(p => !p.WorkExperiences.Any()).ToList();
        var unlinkedBlock = unlinked.Count > 0
            ? string.Join("\r\n\r\n", unlinked.Select(p =>
            {
                var tech = DeserializeTechStack(p.TechStack);
                var sb = new StringBuilder();
                sb.AppendLine($"Project: {p.Title} | Role: {p.Role} | Featured: {p.IsFeatured}");
                if (!string.IsNullOrWhiteSpace(p.Summary)) sb.AppendLine($"Summary: {p.Summary}");
                if (!string.IsNullOrWhiteSpace(p.ImpactStatement)) sb.AppendLine($"Impact: {p.ImpactStatement}");
                if (tech.Count > 0) sb.AppendLine($"Tech: {string.Join(", ", tech)}");
                return sb.ToString().Trim();
            }))
            : null;

        bool hasTailoring = !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(jobDescription);
        var tailorInstruction = hasTailoring
            ? $"""
          TAILORING: Target Role: {title ?? ""}
          {(string.IsNullOrWhiteSpace(jobDescription) ? "" : $"""
          Job Description: {jobDescription}
          - Mirror keywords from the job description
          - Rewrite the summary to target this role specifically
          - Reorder achievements within each role to lead with most relevant
          - In each project summary, emphasize aspects relevant to this role
          - Condense less relevant entries but include ALL entries
          """)}
          """
            : "No tailoring — present all experience neutrally. Featured projects should be prominent.";

        var employerManifest = string.Join(", ", allJobs.Select(j => $"{j.Employer} ({j.Title})"));
        var projectManifest = string.Join(", ", allProjects.Select(p => p.Title));

        const string jsonSchema = """
        {
          "name": "Samuel Ohrenberg",
          "title": "string",
          "summary": "string",
          "coreSkills": ["string"],
          "experience": [
            {
              "employer": "string",
              "title": "string",
              "years": "string or null",
              "summary": "string",
              "achievements": ["string"],
              "projects": [
                {
                  "title": "string",
                  "role": "string",
                  "years": "string or null",
                  "summary": "string",
                  "impact": "string or null",
                  "techStack": ["string"],
                  "isFeatured": false
                }
              ]
            }
          ],
          "additionalProjects": [
            {
              "title": "string",
              "role": "string",
              "years": "string or null",
              "summary": "string",
              "impact": "string or null",
              "techStack": ["string"],
              "isFeatured": false
            }
          ]
        }
        """;

        var systemPrompt = $"""
        You generate structured resume data as JSON for Samuel Ohrenberg.

        COMPLETENESS — NON-NEGOTIABLE:
        You MUST include ALL {allJobs.Count} employer roles: {employerManifest}
        You MUST include ALL {allProjects.Count} projects: {projectManifest}
        If tailoring, condense less relevant entries — never omit them.

        {tailorInstruction}

        OUTPUT: Return ONLY a valid JSON object matching this exact schema.
        No markdown, no code fences, no extra text.

        {jsonSchema}
        """;

        var userContent = new StringBuilder();
        userContent.AppendLine("=== BIOGRAPHICAL & SKILLS INFORMATION ===");
        userContent.AppendLine(infoBlock);
        userContent.AppendLine();
        userContent.AppendLine($"=== PROFESSIONAL EXPERIENCE ({allJobs.Count} roles) ===");
        userContent.AppendLine(jobsBlock);
        if (unlinkedBlock is not null)
        {
            userContent.AppendLine();
            userContent.AppendLine($"=== UNLINKED PROJECTS ({unlinked.Count}) ===");
            userContent.AppendLine(unlinkedBlock);
        }

        try
        {
            var raw = await _llm.CompleteAsync(
                systemPrompt,
                [new LlmMessage("user", userContent.ToString())],
                maxTokens: 8192);

            raw = raw.Trim();
            if (raw.StartsWith("```"))
            {
                raw = string.Join('\n', raw.Split('\n').Skip(1));
                if (raw.TrimEnd().EndsWith("```"))
                    raw = raw[..raw.LastIndexOf("```")].TrimEnd();
                raw = raw.Trim();
            }

            return JsonSerializer.Deserialize<ResumeData>(raw, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate resume data");
            return null;
        }
    }


    private async Task<RoutingResult> RunRoutingPhase(ChatLog chat)
    {
        var receivedAt = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();

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

            ABSOLUTE RULES:
            1. NEVER write tool names in your text response.
            2. NEVER describe what you are about to do.
            3. When calling a tool, your text content must be empty.
            """;

        var messages = BuildMessageList(chat);
        var tools = GetToolDefinitions();

        const int MaxIterations = 6;
        int iteration = 0;
        bool returnResume = false;
        bool tokenLimitReached = false;
        string? redirectToPage = null;
        string? preparedSystemPrompt = null;
        List<LlmMessage>? preparedMessages = null;

        while (iteration++ < MaxIterations)
        {
            LlmResponse result;
            try
            {
                result = await _llm.CompleteWithToolsAsync(
                    systemPrompt, messages, tools, maxTokens: 1000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in routing loop (iteration {Iteration})", iteration);
                return new RoutingResult(null, null, null,
                    "I'm sorry, I encountered an error. Please try again.",
                    false, null, false, receivedAt, stopwatch);
            }

            tokenLimitReached = tokenLimitReached || result.TotalTokens > 2500;

            if (!result.IsToolUse)
            {
                if (preparedSystemPrompt != null)
                    return new RoutingResult(preparedSystemPrompt, preparedMessages, null,
                        null, returnResume, redirectToPage, tokenLimitReached, receivedAt, stopwatch);

                return new RoutingResult(null, null, null,
                    result.Text, returnResume, redirectToPage, tokenLimitReached, receivedAt, stopwatch);
            }

            // Process tool calls
            var toolResults = new List<(string ToolCallId, string Result)>();

            foreach (var toolCall in result.ToolCalls)
            {
                _logger.LogInformation("Routing tool: {ToolName} (iteration {Iteration})",
                    toolCall.Name, iteration);

                string toolResultText;

                switch (toolCall.Name)
                {
                    case "askQuestion":
                        {
                            var question = GetStringArg(toolCall.ArgumentsJson, "question");
                            var (sysPrompt, msgs, shortCircuit) = await BuildQuestionRequest(messages, systemPrompt, question);

                            if (shortCircuit != null)
                                return new RoutingResult(null, null, null, shortCircuit,
                                    returnResume, redirectToPage, tokenLimitReached, receivedAt, stopwatch);

                            preparedSystemPrompt = sysPrompt;
                            preparedMessages = msgs;
                            toolResultText = "Answer prepared for streaming.";
                            break;
                        }

                    case "askClarification":
                        {
                            var question = GetStringArg(toolCall.ArgumentsJson, "question")
                                ?? "Could you tell me a bit more about what you're looking for?";
                            return new RoutingResult(null, null, null, question,
                                returnResume, redirectToPage, tokenLimitReached, receivedAt, stopwatch);
                        }

                    case "contactSamuel":
                        {
                            var email = GetStringArg(toolCall.ArgumentsJson, "email");
                            var msg = GetStringArg(toolCall.ArgumentsJson, "message");
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
                            return new RoutingResult(null, null, null, confirmation,
                                false, null, tokenLimitReached, receivedAt, stopwatch);
                        }

                    case "getResume":
                        returnResume = true;
                        toolResultText = "Resume retrieved successfully. Inform the user their resume is ready.";
                        break;

                    case "redirectToPage":
                        redirectToPage = GetStringArg(toolCall.ArgumentsJson, "page");
                        toolResultText = $"User is being redirected to the {redirectToPage} page.";
                        break;

                    default:
                        _logger.LogWarning("Unrecognized tool: {ToolName}", toolCall.Name);
                        toolResultText = $"Unknown tool '{toolCall.Name}'.";
                        break;
                }

                toolResults.Add((toolCall.ToolCallId, toolResultText));
            }

            messages = messages.Append(new LlmMessage("assistant",
                string.Join("\n", result.ToolCalls.Select(tc =>
                    $"[Tool: {tc.Name}, Args: {tc.ArgumentsJson}]")))).ToList();

            foreach (var (toolCallId, toolResult) in toolResults)
            {
                messages = messages.Append(new LlmMessage("user",
                    $"[Tool result for {toolCallId}]: {toolResult}")).ToList();
            }
        }

        return new RoutingResult(null, null, null,
            "I'm sorry, I had trouble forming a response. Please try again.",
            false, null, tokenLimitReached, receivedAt, stopwatch);
    }


    private async Task<(string SystemPrompt, List<LlmMessage> Messages, string? ShortCircuit)>
        BuildQuestionRequest(
            List<LlmMessage> conversationHistory,
            string baseSystemPrompt,
            string? explicitQuestion)
    {
        var queryText = !string.IsNullOrWhiteSpace(explicitQuestion)
            ? explicitQuestion
            : BuildUserQueryText(conversationHistory);

        if (queryText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3)
        {
            return (baseSystemPrompt, [], "Hi there! I'm SamuelLM, Sam's portfolio assistant. Feel free to ask me anything about his background, skills, or experience!");
        }

        var tokens = Tokenizer.Tokenize(queryText).ToList();
        var relevantInfo = await GetRelevantInformation(tokens, queryText);

        if (string.IsNullOrWhiteSpace(relevantInfo))
        {
            await CreateInformationRequest(tokens);
            return (baseSystemPrompt, [], "I'm sorry, I don't have information about that topic yet. I've made a note of the gap and will work to get it added!");
        }

        var enrichedSystemPrompt = $"""
        {baseSystemPrompt}

        ════════════════════════════════════════════
        CONTEXT — use ONLY the information below to answer.
        Do NOT use your training data under any circumstances.
        Do NOT invent projects, employers, technologies, dates, or outcomes.
        ════════════════════════════════════════════

        {relevantInfo}

        ════════════════════════════════════════════
        RESPONSE RULES:
        - Maximum {MaxResponseWords} words. Be concise.
        - Plain text only — no markdown
        - Speak as Samuel in first person, professional but brief
        - Never add closing offers like "Would you like to know more?"
        ════════════════════════════════════════════
        """;

        var cleanHistory = conversationHistory
            .Where(m => !m.Content.StartsWith("[Tool:") && !m.Content.StartsWith("[Tool result"))
            .ToList();

        return (enrichedSystemPrompt, cleanHistory, null);
    }


    private async Task<string> GetRelevantInformation(IEnumerable<string> tokens, string queryText)
    {
        var tokenList = tokens.Where(t => t.Length >= MinTokenLength).ToList();
        var queryEmbedding = await _embeddings.GetEmbeddingAsync(queryText);

        List<Information> top;

        if (queryEmbedding != null)
        {
            var queryVector = new Pgvector.Vector(queryEmbedding);

            // Query all three tables in parallel using pgvector native similarity
            await using var ctx1 = await _dbContextFactory.CreateDbContextAsync();
            await using var ctx2 = await _dbContextFactory.CreateDbContextAsync();
            await using var ctx3 = await _dbContextFactory.CreateDbContextAsync();

            var infoTask = ctx1.Information
                .Include(i => i.Keywords)
                .Where(i => i.Embedding != null)
                .OrderBy(i => i.Embedding!.CosineDistance(queryVector))
                .Take(MaxContextEntries)
                .ToListAsync();

            var projectsTask = ctx2.Projects
                .Include(p => p.WorkExperiences)
                .Where(p => p.IsActive && p.Embedding != null)
                .OrderBy(p => p.Embedding!.CosineDistance(queryVector))
                .Take(MaxContextEntries)
                .ToListAsync();

            var workTask = ctx3.WorkExperiences
                .Where(w => w.IsActive && w.Embedding != null)
                .OrderBy(w => w.Embedding!.CosineDistance(queryVector))
                .Take(MaxContextEntries)
                .ToListAsync();

            await Task.WhenAll(infoTask, projectsTask, workTask);

            var informations = await infoTask;
            var projects = await projectsTask;
            var workEntries = await workTask;

            // Convert projects and work to Information objects for unified ranking
            var projectInfos = projects.Select(BuildProjectInformation).ToList();
            var workInfos = workEntries.Select(BuildWorkExperienceInformation).ToList();
            var allEntries = informations.Concat(projectInfos).Concat(workInfos).ToList();

            // Re-rank the combined pool by cosine similarity in memory
            // (pgvector already narrowed each table down to top N candidates)
            top = allEntries
                .Select(entry =>
                {
                    var entryEmbedding = entry.Embedding?.ToArray();

                    float score = entryEmbedding != null
                        ? _embeddings.CosineSimilarity(queryEmbedding, entryEmbedding)
                        : 0f;

                    return (entry, score);
                })
                .OrderByDescending(x => x.score)
                .Take(MaxContextEntries)
                .Select(x => x.entry)
                .ToList();
        }
        else
        {
            // Fallback: no embedding available, use keyword scoring
            _logger.LogWarning("Query embedding unavailable, falling back to keyword scoring");

            await using var ctx1 = await _dbContextFactory.CreateDbContextAsync();
            await using var ctx2 = await _dbContextFactory.CreateDbContextAsync();
            await using var ctx3 = await _dbContextFactory.CreateDbContextAsync();

            var informationTask = ctx1.Information.Include(i => i.Keywords).ToListAsync();
            var projectsTask = ctx2.Projects.Include(p => p.WorkExperiences).Where(p => p.IsActive).ToListAsync();
            var workTask = ctx3.WorkExperiences.Where(j => j.IsActive).ToListAsync();

            await Task.WhenAll(informationTask, projectsTask, workTask);

            var allEntries = (await informationTask)
                .Concat((await projectsTask).Select(BuildProjectInformation))
                .Concat((await workTask).Select(BuildWorkExperienceInformation))
                .ToList();

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

            var scored = allEntries
                .Select(i => (Information: i, Score: ScoreEntry(i, tokenList)))
                .ToList();

            top = scored
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(MaxContextEntries)
                .Select(x => x.Information)
                .ToList();

            if (top.Count == 0)
                top = scored
                    .OrderByDescending(x => x.Score)
                    .Take(MaxContextEntries)
                    .Select(x => x.Information)
                    .ToList();
        }

        return BuildContextBlock(top);
    }


    private async Task<string> GetContactConfirmationMessage(string? email, string? msg, string? error)
    {
        const string systemPrompt = """
            You are an AI assistant on Samuel Ohrenberg's portfolio website.
            A user has just submitted a contact request. Generate a short, friendly confirmation message.
            If an error occurred, acknowledge it politely and suggest they try again.
            Output only the message text. Keep it under 60 words.
            """;

        var userContent = $"""
            Email provided: {email ?? "(not provided)"}
            Message from user: {msg ?? "(none)"}
            Result: {(string.IsNullOrEmpty(error) ? "Successfully sent to Samuel." : $"Error — {error}")}
            """;

        try
        {
            return await _llm.CompleteAsync(
                systemPrompt,
                [new LlmMessage("user", userContent)],
                maxTokens: 150);
        }
        catch
        {
            return "Your contact request was received! Samuel will be in touch soon.";
        }
    }


    private static LlmToolDefinition[] GetToolDefinitions() =>
    [
        new("contactSamuel",
            "Sends a contact request to Samuel Ohrenberg on behalf of the user.",
            new
            {
                type = "object",
                properties = new
                {
                    email = new { type = "string", description = "The user's email address." },
                    message = new { type = "string", description = "Optional message from the user." }
                },
                required = new[] { "email" }
            }),

        new("redirectToPage",
            "Redirects the user to a relevant page. Available: 'Contact', 'Resume', 'Projects'.",
            new
            {
                type = "object",
                properties = new
                {
                    page = new { type = "string", description = "One of: 'Contact', 'Projects', 'Resume'." }
                },
                required = new[] { "page" }
            }),

        new("getResume",
            "Returns Samuel's resume for the user to view or download.",
            new { type = "object", properties = new { } }),

        new("askClarification",
            "Ask the user a clarifying question when their request is genuinely ambiguous.",
            new
            {
                type = "object",
                properties = new
                {
                    question = new { type = "string", description = "The clarifying question to ask." }
                },
                required = new[] { "question" }
            }),

        new("askQuestion",
            "Answers specific questions about Samuel's technical skills, work experience, projects, and background.",
            new
            {
                type = "object",
                properties = new
                {
                    question = new { type = "string", description = "The full question being asked." }
                },
                required = new[] { "question" }
            }),
    ];


    private static float ScoreEntry(Information entry, IReadOnlyList<string> tokens)
    {
        int matches = entry.Keywords.Count(k =>
        {
            var kText = k.Text.ToLower();
            if (tokens.Any(t => t == kText)) return true;
            if (kText.Length < MinTokenLength) return false;
            return tokens.Any(t => t.Length >= MinTokenLength &&
                Utility.LevenshteinDifference(kText, t) <= 25);
        });

        if (matches == 0) return 0;
        float density = (float)matches / Math.Max(entry.Keywords.Count, 1);
        float jitter = Utility.TrueRandom(1, 10);
        return (density * 70) + jitter;
    }

    private static Information BuildProjectInformation(Project project)
    {
        var text = BuildProjectRagText(project);
        var techStack = DeserializeTechStack(project.TechStack);
        var employerNames = string.Join(' ', project.WorkExperiences?.Select(w => w.Employer) ?? []);

        var keywords = techStack
            .Select(t => new Keyword(t.ToLower(), null!))
            .Concat(Tokenizer.Tokenize(
                $"{project.Title} {employerNames} {project.Role} {project.Summary} {project.Detail} {project.ImpactStatement}")
                .Select(t => new Keyword(t, null!)))
            .ToList();

        var info = new Information(project.ProjectId, text, keywords);
        info.Embedding = project.Embedding;
        if (project.Embedding != null)
            info.Embedding = project.Embedding;
        return info;
    }

    private static Information BuildWorkExperienceInformation(WorkExperience job)
    {
        var text = BuildWorkRagText(job);
        List<string> achievements;
        try { achievements = JsonSerializer.Deserialize<List<string>>(job.Achievements) ?? []; }
        catch { achievements = []; }

        var keywords = Tokenizer
            .Tokenize($"{job.Title} {job.Employer} {job.Summary} {string.Join(' ', achievements)}")
            .Select(t => new Keyword(t, null!))
            .ToList();

        var info = new Information(job.WorkExperienceId, text, keywords);
        info.Embedding = job.Embedding;
        if (job.Embedding != null)
            info.Embedding = job.Embedding;
        return info;
    }

    private static string BuildContextBlock(IEnumerable<Information> entries)
    {
        var sb = new StringBuilder();
        foreach (var info in entries)
            if (!string.IsNullOrWhiteSpace(info.Text))
                sb.AppendLine(info.Text).AppendLine();
        return sb.ToString().Trim();
    }

    private async Task CreateInformationRequest(IEnumerable<string> tokens)
    {
        var information = new Information(null, tokens);
        await _dbContext.AddAsync(information);
        await _dbContext.SaveChangesAsync();
    }


    private async Task SaveChatLog(ChatLog chatLog, string message, bool error,
        bool tokenLimitReached, DateTimeOffset receivedAt, long elapsedMilliseconds)
    {
        var chat = new Chat
        {
            History = chatLog.PrintHistory(),
            Message = chatLog.Message,
            Response = message,
            ReceivedAt = receivedAt.ToUniversalTime(),
            ResponseTookMs = elapsedMilliseconds,
            Error = error,
            TokenLimitReached = tokenLimitReached,
            SessionTrackingId = chatLog.UserTrackingId
        };

        await _dbContext.AddAsync(chat);
        await _dbContext.SaveChangesAsync();
    }


    private static List<LlmMessage> BuildMessageList(ChatLog chat)
    {
        var messages = chat.History
            .Select(h => new LlmMessage(h.Role == "user" ? "user" : "assistant", h.Content))
            .ToList();
        messages.Add(new LlmMessage("user", chat.Message));
        return messages;
    }

    private static string BuildUserQueryText(IEnumerable<LlmMessage> history)
    {
        var sb = new StringBuilder();
        foreach (var msg in history.Where(m => m.Role == "user"))
            sb.Append(msg.Content).Append(' ');
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

    private static string? GetStringArg(string argumentsJson, string key)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.TryGetProperty(key, out var val))
                return val.GetString();
        }
        catch { }
        return null;
    }

    internal static string BuildProjectRagText(Project project)
    {
        var techStack = DeserializeTechStack(project.TechStack);
        var years = BuildYearRange(project.StartYear, project.EndYear);
        var employers = project.WorkExperiences?.Select(w => w.Employer).ToList() ?? [];

        var sb = new StringBuilder();
        sb.AppendLine($"Project: {project.Title}");
        if (employers.Count > 0)
            sb.AppendLine($"Employer(s): {string.Join(", ", employers)}");
        sb.AppendLine($"Role: {project.Role}");
        if (years != null) sb.AppendLine($"Years: {years}");
        sb.AppendLine($"Summary: {project.Summary}");
        if (!string.IsNullOrWhiteSpace(project.Detail))
            sb.AppendLine($"Detail: {project.Detail}");
        if (!string.IsNullOrWhiteSpace(project.ImpactStatement))
            sb.AppendLine($"Impact: {project.ImpactStatement}");
        if (techStack.Count > 0)
            sb.AppendLine($"Tech Stack: {string.Join(", ", techStack)}");

        return sb.ToString().Trim();
    }

    internal static string BuildWorkRagText(WorkExperience job)
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
            foreach (var a in achievements)
                sb.AppendLine($"  - {a}");
        }
        return sb.ToString().Trim();
    }
}
