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
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PortfolioWebsite.Api.Services;

public class ChatService
{
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
            - contactSamuel: when the user wants to get in touch with Sam
            - getResume: when the user wants to see or download Sam's resume
            - redirectToPage: when the user wants content best found on a specific page

            IMPORTANT: Never describe, mention, narrate, or reference tool calls in your response.
            Never say things like "I'll call askQuestion" or "askQuestion: ..." or "I'm going to use a tool".
            Tool use happens silently behind the scenes. Your response to the user should only ever be
            the final answer — never the process of getting there.

            CRITICAL: Your response to the user must NEVER mention tools, retrieval, or looking things up.
            Do not say things like:
            - "I'll retrieve information..."
            - "Let me look that up..."
            - "I'm going to check..."
            - "I'll find out about..."
            If you are calling a tool, output NO text alongside it — leave the text content completely empty.
            Your only job in this phase is to silently route to the correct tool.
            The actual answer will be generated separately.
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

        var request = new ConverseRequest
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
        string? inlineText = null;
        bool toolWasCalled = false;

        foreach (var block in result.Output.Message.Content)
        {
            if (block.Text != null)
                inlineText = block.Text;

            if (block.ToolUse == null)
                continue;

            toolWasCalled = true;

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
                        var questionResponse = await AskBedrockQuestion(messages, systemPrompt, null);
                        message = questionResponse.Message;
                        error = error || questionResponse.Error;
                        tokenLimitReached = tokenLimitReached || questionResponse.TokenLimitReached;
                        break;
                    }

                case "askQuestion":
                    {
                        var question = GetStringArg(toolInput, "question");
                        var questionResponse = await AskBedrockQuestion(messages, systemPrompt, question);
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

        if (!toolWasCalled && !string.IsNullOrWhiteSpace(inlineText))
            message = inlineText;

        if (string.IsNullOrWhiteSpace(message))
            message = "I'm sorry, I wasn't sure how to handle that. Could you rephrase your question?";

        return new ChatResponse(message, error, tokenLimitReached, returnResume, redirectToPage);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Bedrock: Q&A with RAG
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<ChatResponse> AskBedrockQuestion(
        List<Message> conversationHistory,
        string baseSystemPrompt,
        string? explicitQuestion)
    {
        var queryText = !string.IsNullOrWhiteSpace(explicitQuestion)
            ? explicitQuestion
            : BuildUserQueryText(conversationHistory);

        if (queryText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3)
        {
            return new ChatResponse(
                "Hi there! I'm SamuelLM, Sam's portfolio assistant. Feel free to ask me anything about his background, skills, or experience!",
                false);
        }

        var tokens = Tokenizer.Tokenize(queryText).ToList();
        var relevantInfo = await GetRelevantInformation(tokens);

        if (string.IsNullOrWhiteSpace(relevantInfo))
        {
            await CreateInformationRequest(tokens);
            return new ChatResponse(
                "I'm sorry, I don't have information about that topic yet. " +
                "I've made a note of the gap and will work to get it added!",
                false);
        }

        var enrichedSystemPrompt = $"""
            {baseSystemPrompt}

            Use the following curated excerpts from Samuel Ohrenberg's professional background to answer
            the user's question. These are the most relevant sections available:

            {relevantInfo}

            Guidelines for your response:
            - Answer concisely and professionally, as if Samuel is speaking in an interview
            - Keep your answer under {MaxResponseWords} words
            - Do not output markdown — use plain text only
            - If the provided context does not contain enough information to answer the question
              confidently, say so honestly rather than guessing
            - Do not invent project names, employers, dates, technologies, or outcomes that are
              not present in the context above
            """;

        var request = new ConverseRequest
        {
            ModelId = _questions.Model,
            System = [new SystemContentBlock { Text = enrichedSystemPrompt }],
            Messages = conversationHistory,
            InferenceConfig = new InferenceConfiguration { MaxTokens = MaxResponseTokens }
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