using Azure;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos;
using PortfolioWebsite.Api.Services.Entities;
using PortfolioWebsite.Api.Services.Entities.Google;
using PortfolioWebsite.Common;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using static PortfolioWebsite.Api.Controllers.ChatController;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PortfolioWebsite.Api.Services;

public class ChatService
{
    private const int MatchWeight = 60;
    private readonly ILogger<ChatService> _logger;
    private readonly SqlDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly ContactService _contactService;
    private readonly ModelSettings _toolUse;
    private readonly ModelSettings _questions;
    private readonly string? _geminiApiKey;
    private readonly string _lmUrl;

    public bool geminiOverLimit { get; private set; }

    private record ModelSettings(string Model, float Temperature);

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, SqlDbContext dbContext, HttpClient httpClient, ContactService contactService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _httpClient = httpClient;
        _contactService = contactService;
        _lmUrl = configuration.GetValue<string>("ChatSettings:Url") ?? throw new NullReferenceException("ChatSettings:Url must be populated in settings");

        string model = configuration.GetValue<string>("ChatSettings:ToolUse:Model") ?? throw new NullReferenceException("ChatSettings:ToolUse:Model must be populated in settings");
        float temperature = configuration.GetValue<float?>("ChatSettings:ToolUse:Temperature") ?? throw new NullReferenceException("ChatSettings:ToolUse:Temperature must be populated in settings");

        _toolUse = new ModelSettings(model, temperature);

        model = configuration.GetValue<string>("ChatSettings:Question:Model") ?? throw new NullReferenceException("ChatSettings:Question:Model must be populated in settings");
        temperature = configuration.GetValue<float?>("ChatSettings:Question:Temperature") ?? throw new NullReferenceException("ChatSettings:Question:Temperature must be populated in settings");

        _questions = new ModelSettings(model, temperature);

        _geminiApiKey = configuration.GetValue<string>("Gemini:Apikey");

    }

    internal async Task<ChatResponse> QueryChat(ChatLog chat)
    {
        DateTimeOffset receivedAt = DateTimeOffset.Now;
        Stopwatch stopwatch = new();
        stopwatch.Start();


        ChatResponse response = null!;

        if (!string.IsNullOrEmpty(_geminiApiKey) && !geminiOverLimit)
        {
            response = await QueryGemini(chat);
        }
        else
        {
            response = await QueryLocalChat(chat);
        }



        // save the chat log
        stopwatch.Stop();
        _logger.LogInformation($"Chat query took {stopwatch.ElapsedMilliseconds}ms");
        await SaveChatLog(chat, response.Message, response.Error, response.TokenLimitReached, receivedAt, stopwatch.ElapsedMilliseconds);

        return response;
    }

    private async Task<ChatResponse> QueryGemini(ChatLog chat)
    {
        var request = new GoogleChatRequest
        {
            GenerationConfig = new GenerationConfig
            {
                Temperature = _toolUse.Temperature,
                TopK = 40,
                TopP = 0.95,
                MaxOutputTokens = 1000,
                ResponseMimeType = "text/plain"
            },
            SystemInstruction = new Content
            {
                Role = "user",
                Parts =
                [
                    new()
                    {
                        Text = """
                            Context: 
                                You are an AI chatbot. 
                                Your name is SamuelLM. 
                                You were created by Samuel Ohrenberg, who also goes by Sam or Sammy. 
                                Your URL is https://aboutsamuel.com/. 
                                You are a Large Language Model, or LLM. 
                                You are hosted off of Sam's computer to provide an AI assistant for his portfolio. 
                                I am interfacing with you via an ASP.NET Core Web API and a Vue.js web application.
                                You are answering on behalf of Samuel. 
                                Any questions asked of you should be answered as if you were Samuel.
                                Any time I ask you a question, you should answer as if you were in an interview.
                                You are a professional, friendly, and helpful assistant.
                        """
                    }
                ]
            },
            Contents = chat.History.Select(h => new Content
            {
                Role = h.Role == "user" ? "user" : "model",
                Parts = new List<Part>
                {
                    new Part
                    {
                        Text = h.Content
                    }
                }
            }).ToList(),
            ToolConfig = new()
            {
                FunctionCallingConfig = new()
                {
                    Mode = "ANY"
                }
            },
            Tools = new Tools
            {
                FunctionDeclarations =
                [
                    new() {
                        Name = "contactSamuel",
                        Description = "Accepts the email of the user and an optional message from the user that generates a contact request for Samuel Ohrenberg. This allows users to contact Samuel in case they have further questions or wish to discuss something",
                        Parameters = new FunctionParameters
                        {
                            Type = "object",
                            Properties = new Dictionary<string, FunctionProperty>
                            {
                                {
                                    "email"
                                    , new FunctionProperty
                                    {
                                        Type = "string",
                                        Description = "The user's email address that Samuel will use to contact them. This is a required field and must be provided by the user."
                                    }
                                },
                                {
                                    "message"
                                    , new FunctionProperty 
                                    {
                                        Type = "string",
                                        Description = "An optional short message from the user explaining what the contact request is for."
                                    }
                                }
                            }
                        }
                    },
                    new()
                    {
                        Name = "redirectToPage",
                        Description = @"Checks if the user is wanting information that can be found on one of the following pages: 'Contact', 'Testimonial', 'Resume'. 
                                        The contact page contains a form that the user can use to reach out to Sam.
                                        The testimonial page contains testimonials from coworkers and past partners about Samuel.
                                        The resume page contains information about Sam's work history, projects, education, and skills.",
                        Parameters = new()
                        {
                            Type = "object",
                            Properties = new()
                            {
                                {
                                    "page",
                                    new()
                                    {
                                        Type = "string",
                                        Description = "That page that contains actions or information pertinent to the user's query"
                                    }
                                }
                            }
                        }
                    },
                    new()
                    {
                        Name = "getResume",
                        Description = "Returns the resume of Samuel Ohrenberg in PDF format"
                    },
                    new()
                    {
                        Name = "askQuestion",
                        Description = "Accepts any question from the user and returns an answer. This includes any technical question, personal question, interview question, or anything. ",
                        Parameters = new()
                        {
                            Type = "object",
                            Properties = new()
                            {
                                {
                                    "question",
                                    new()
                                    {
                                        Type = "string",
                                        Description = "The question that the user is asking, e.g. 'How do you ensure the code you write is maintainable and scalable?' or 'Do you know C#'?"
                                    }
                                }
                            }
                        }
                    }
                ]

            }
        };

        request.Contents.Add(new Content
        {
            Role = "user",
            Parts =
            [
                new()
                {
                    Text = chat.Message
                }
            ]
        });

        var response = await GetGoogleChatResponse(request) ?? throw new Exception("Something went wrong");

        if (response.ToolCalls is not null)
        {
            foreach (var toolCall in response.ToolCalls)
            {
                switch (toolCall.Name)
                {
                    case "contactSamuel":
                        var email = toolCall.Arguments?["email"]?.ToString();
                        var msg = toolCall.Arguments?["message"]?.ToString();
                        string? error = null;
                        try
                        {
                            if (!string.IsNullOrEmpty(email))
                            {
                                await _contactService.SendContactRequest(email, msg);
                            }
                            else
                            {
                                error = "No email was provided.";
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error sending contact request");
                            error = "An unknown system error occurred.";
                        }
                        finally
                        {
                            response.Message = await GetGoogleContactRequestMessage(email, msg, error);
                        }
                        break;
                    case "getResume":
                        response.ReturnResume = true;
                        response.Message = "Of course. Here is Sam's resume!";
                        break;
                    case "redirectToPage":
                        {
                            response.RedirectToPage = toolCall.Arguments?["page"]?.ToString();
                            GoogleChatRequest questionCompletion = new()
                            {
                                SystemInstruction = request.SystemInstruction,
                                Contents = request.Contents,
                                GenerationConfig = request.GenerationConfig
                            };

                            questionCompletion.GenerationConfig.Temperature = 0.8;
                            questionCompletion.GenerationConfig.MaxOutputTokens = 75;

                            var questionResponse = await AskGoogleQuestion(questionCompletion);
                            response.Message = questionResponse.Message;
                            response.Error = response.Error || questionResponse.Error;
                            response.TokenLimitReached = response.TokenLimitReached || questionResponse.TokenLimitReached;
                        }
                        break;
                    case "askQuestion":
                        string? question = toolCall.Arguments?["question"]?.ToString();
                        if (!string.IsNullOrEmpty(question))
                        {
                            GoogleChatRequest questionCompletion = new()
                            {
                                SystemInstruction = request.SystemInstruction,
                                Contents = request.Contents,
                                GenerationConfig = request.GenerationConfig
                            };
                            questionCompletion.GenerationConfig.Temperature = 0.8;
                            questionCompletion.GenerationConfig.MaxOutputTokens = 200;
                            var questionResponse = await AskGoogleQuestion(questionCompletion);
                            response.Message = questionResponse.Message;
                            response.Error = response.Error || questionResponse.Error;
                            response.TokenLimitReached = response.TokenLimitReached || questionResponse.TokenLimitReached;
                        }
                        break;
                }
            }
        }

        return response;
    }

    private async Task<ChatResponse> AskGoogleQuestion(GoogleChatRequest completion)
    {
        var fullUserChatLogBuilder = new StringBuilder();
        foreach (var entry in completion.Contents)
        {
            if (entry.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var part in entry.Parts)
                {
                    if (part.Text is not null)
                    {
                        fullUserChatLogBuilder.Append(part.Text).Append(' ');
                    }
                }
            }
        }
        var tokens = Tokenizer.Tokenize(fullUserChatLogBuilder.ToString());
        var relevantInfo = await GetRelevantInformation(tokens);

        if (relevantInfo is null || relevantInfo.Length == 0)
        {
            await CreateInformationRequest(tokens);
            return new ChatResponse("I'm sorry, I don't have that information. I've made a note of it and will work to get it added.", false);
        }

        // add the new
        completion.SystemInstruction.Parts.First().Text += $"""
            Use the following information from Samuel Ohrenberg's resume to answer any questions about him. 
            Please note, this is only a small selection from his resume and there is likely even more that would be relevant: 
            {relevantInfo}
        """;

        completion.SystemInstruction.Parts.First().Text += $"\r\nPlease respond very concisely and professionally, as if you were in an interview. ";

        var response = await GetGoogleChatResponse(completion);

        return response;

    }

    private async Task<string> GetGoogleContactRequestMessage(string? email, string? msg, string? error)
    {
        var request = new GoogleChatRequest
        {
            SystemInstruction = new Content
            {
                Role = "user",
                Parts = [
                    new () {
                        Text = $"""
                            
                        Context:  
                            You are an AI chatbot on Samuel Ohrenberg's portfolio website, aboutsamuel.com. 
                            Users provide you with an email and an optional message and you generate the confirmation message that they receive in their chat window.
                            The target audience that you generate responses for is the user that is sending the contact request.
                            You should be friendly, professional, and helpful.
                            You should be concise and to the point.
                            You should generate messages that are intended for posting in a chat window.
                            I will provide you with the email, a message from the user, and, if there was an issue, what the error was..
                            You should generate a fairly standard message that I can return to the user to let them know I received their request
                            and, if an error occurred, that there was an issue. Only output the message that I will give to the user - nothing else.

                    """
                    }
                ]
            },
            GenerationConfig = new GenerationConfig
            {
                MaxOutputTokens = 100,
                Temperature = 0.8,
                ResponseMimeType = "text/plain",
                TopK = 40,
                TopP = 0.95
            },
            Contents = 
            [
                new() 
                {
                    Role = "user",
                    Parts = 
                    [
                        new() 
                        {
                            Text = $"""
                                Email: { email }
                                Message: { msg }
                                { (string.IsNullOrEmpty(error) ? "There was no error. I successfully received the request." : $"Error: { error }") }
                            """
                        }
                    ]
                }
            ]
        };

        var response = await GetGoogleChatResponse(request) ?? throw new Exception("Something went wrong");
        return response.Message;
    }

    private async Task<ChatResponse?> GetGoogleChatResponse(GoogleChatRequest request)
    {
        var requestJson = System.Text.Json.JsonSerializer.Serialize(request);
        var response = await _httpClient.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_geminiApiKey}", new StringContent(requestJson, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        var responseText = await response.Content.ReadAsStringAsync();
        var responseJson = System.Text.Json.JsonSerializer.Deserialize<GoogleChatResponse>(responseText);

        string message = string.Empty;
        bool error = false;
        bool tokenLimitReached = responseJson.UsageMetadata.TotalTokenCount > 2500;
        List<ToolCall>? toolCall = null;

        foreach (var candidate in responseJson?.Candidates ?? [])
        {
            foreach (var part in candidate.Content.Parts)
            {
                if (part.FunctionCall is not null)
                {
                    toolCall ??= [];
                    toolCall.Add(new ToolCall()
                    {
                        Name = part.FunctionCall.Name,
                        Arguments = JObject.FromObject(part.FunctionCall.Args)
                    });
                }
                
                if (part.Text is not null)
                {
                    if (!string.IsNullOrEmpty(message))
                    {
                        message += ' ';
                    }
                    message += part.Text;
                }
            }
        }


        ChatResponse chatResponse = new ChatResponse(message, error, tokenLimitReached, toolCalls: toolCall);

        return chatResponse;
    }

    private async Task<ChatResponse> QueryLocalChat(ChatLog chat)
    {
        var completion = new
        {
            model = _toolUse.Model,
            temperature = _toolUse.Temperature,
            messages = new List<ChatMessage>
            {
                new ChatMessage("system", $"""
                            
                        Context:  
                            You are an AI chatbot. Your name is SamuelLM.                         
                            You were created by Samuel Ohrenberg, who also goes by Sam or Sammy.
                            Your URL is https://aboutsamuel.com/
                            You are a Large Language Model, or LLM.
                            You are hosted off of Sam's computer to provide an AI assistant for his portfolio. 
                            I am interfacing with you via an ASP.NET Core Web API and a Vue.js web application. 
                            You are answering on behalf of Samuel.
                            You are a professional, friendly, and helpful assistant.
                            You should identify which tool calls to make based on the user's input.
                            There can be multiple tool calls in a single response.
                            You should only use the tools that are available to you.
                            You should output a message even if a tool call is required.
                            Do not output markdown. Only output raw text.

                    """
                )
            },
            tools = Constants.SupportedTools
        };

        foreach (var entry in chat.History)
        {
            completion.messages.Add(entry);
        }
        completion.messages.Add(new ChatMessage("user", chat.Message));

        // request the chat
        var response = await GetChatResponse(completion);

        if (response.ToolCalls is not null)
        {
            foreach (var toolCall in response.ToolCalls)
            {
                switch (toolCall.Name)
                {
                    case "contactSamuel":
                        var email = toolCall.Arguments?["email"]?.ToString();
                        var msg = toolCall.Arguments?["message"]?.ToString();
                        string? error = null;
                        try
                        {
                            if (!string.IsNullOrEmpty(email))
                            {
                                await _contactService.SendContactRequest(email, msg);
                            }
                            else
                            {
                                error = "No email was provided.";
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error sending contact request");
                            error = "An unknown system error occurred.";
                        }
                        finally
                        {
                            response.Message = await GetContactRequestMessage(email, msg, error);
                        }
                        break;
                    case "getResume":
                        response.ReturnResume = true;
                        response.Message = "Of course. Here is Sam's resume!";
                        break;
                    case "redirectToPage":
                        {
                            response.RedirectToPage = toolCall.Arguments?["page"]?.ToString();
                            var questionCompletion = new
                            {
                                model = _questions.Model,
                                completion.messages,
                                temperature = _questions.Temperature
                            };

                            var questionResponse = await AskQuestion(questionCompletion);
                            response.Message = questionResponse.Message;
                            response.Error = response.Error || questionResponse.Error;
                            response.TokenLimitReached = response.TokenLimitReached || questionResponse.TokenLimitReached;
                        }
                        break;
                    case "askQuestion":
                        string? question = toolCall.Arguments?["question"]?.ToString();
                        if (!string.IsNullOrEmpty(question))
                        {
                            var questionCompletion = new
                            {
                                model = _questions.Model,
                                completion.messages,
                                temperature = _questions.Temperature
                            };
                            var questionResponse = await AskQuestion(questionCompletion);
                            response.Message = questionResponse.Message;
                            response.Error = response.Error || questionResponse.Error;
                            response.TokenLimitReached = response.TokenLimitReached || questionResponse.TokenLimitReached;
                        }
                        break;
                }
            }
        }

        return response;
    }

    private async Task<string> GetPageRedirectMessage(string message, string? redirectToPage)
    {
        var completion = new
        {
            model = _questions.Model,
            temperature = _questions.Temperature,
            messages = new List<ChatMessage>
            {
                new ChatMessage("system", $"""
                            
                        Context:  
                            You are an AI chatbot.
                            In a chat window, the end user has posted a message that has been determined there is a page on my website that contains information about their post.
                            The target audience that you generate responses for is the user that is being redirected to the page.
                            You should be friendly, professional, and helpful.
                            You should be concise and to the point.
                            You should generate messages that are intended for posting in a chat window.
                            I will provide you with the message from the user and the page that they are being directed to.
                            You should generate a fairly standard message explaining them the redirect.
                            You do not need to generate a link for the user. They are being redirected automatically.
                            You only need to generate the message explaining the redirect.

                    """
                ),
                new ChatMessage("user", $"""
                    User's message that was redirect to a page: { message }
                    Page they are being directed to: { redirectToPage }
                    """
                )
            },
        };

        var response = await GetChatResponse(completion);
        return response.Message;
    }

    private async Task<string> GetContactRequestMessage(string? email, string? msg, string? error)
    {
        var completion = new
        {
            model = _questions.Model,
            temperature = _questions.Temperature,
            messages = new List<ChatMessage>
            {
                new ChatMessage("system", $"""
                            
                        Context:  
                            You are an AI chatbot. Users provide you with an email and an optional message and you generate the confirmation message that they receive in their chat window.
                            The target audience that you generate responses for is the user that is sending the contact request.
                            You should be friendly, professional, and helpful.
                            You should be concise and to the point.
                            You should generate messages that are intended for posting in a chat window.
                            I will provide you with the email, a message from the user, and, if there was an issue, what the error was..
                            You should generate a fairly standard message that I can return to the user to let them know I received their request
                            and, if an error occurred, that there was an issue. Only output the message that I will give to the user - nothing else.

                    """
                ),
                new ChatMessage("user", $"""
                    Email: { email }
                    Message: { msg }
                    { (string.IsNullOrEmpty(error) ? "There was no error. I successfully received the request." : $"Error: { error }") }
                    """
                )
            },
        };

        var response = await GetChatResponse(completion);
        return response.Message;
    }

    private async Task SaveChatLog(ChatLog chatLog, string message, bool error, bool tokenLimitReached, DateTimeOffset receivedAt, long elapsedMilliseconds)
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

    private async Task<ChatResponse> GetChatResponse(dynamic completion)
    {
        JObject obj = JObject.FromObject(completion);
        string jsonString = obj.ToString();
        StringContent content = new StringContent(jsonString, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_lmUrl, content);
        var responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return new ChatResponse(responseText, true, false);
        }

        var jsonObject = JObject.Parse(responseText);
        IEnumerable<ToolCall> toolCalls = null;
        var responseMessage = jsonObject["choices"]?[0]?["message"];
        if (responseMessage is not null && responseMessage["content"] is not null)
        {
            responseText = responseMessage["content"]?.ToString() ?? string.Empty;
        }
        else
        {
            responseText = string.Empty;
        }
        if (responseText.Contains("</think>"))
        {
            responseText = responseText.Split("</think>")[1].Trim(' ', '\r', '\n');
        }

        if (responseMessage is not null && responseMessage["tool_calls"] is not null && responseMessage["tool_calls"] is JArray toolCallsObj)
        {
            toolCalls = toolCallsObj.Select(a => new ToolCall(a?["function"]?["name"]?.ToString(), a?["function"]?["arguments"]?.ToString()));
        }
        else
        {
            var toolCallsMatch = Regex.Match(responseText, @"\[TOOL_CALLS\](\[.*?\])(.*)");
            if (toolCallsMatch.Success)
            {
                responseText = toolCallsMatch.Groups[2].Value;
                string toolCallsJson = toolCallsMatch.Groups[1].Value;
                JArray toolCallArray = JArray.Parse(toolCallsJson);
                toolCalls = toolCallArray.Select(t => new ToolCall(t["name"]?.ToString(), t["arguments"]?.ToString()));
            }
        }

        return new ChatResponse(responseText, false, Convert.ToInt32(jsonObject["usage"]["total_tokens"]) > 2500, toolCalls: toolCalls); ;

    }

    private async Task<ChatResponse> AskQuestion(dynamic completion)
    {
        var fullUserChatLogBuilder = new StringBuilder();
        foreach (var entry in completion.messages)
        {
            if (entry.Role.Equals("user", StringComparison.OrdinalIgnoreCase) && entry.Content.Length > 0)
                fullUserChatLogBuilder.Append(entry.Content).Append(" ");
        }
        var tokens = Tokenizer.Tokenize(fullUserChatLogBuilder.ToString());
        var relevantInfo = await GetRelevantInformation(tokens);

        if (relevantInfo is null || relevantInfo.Length == 0)
        {
            await CreateInformationRequest(tokens);
            return new ChatResponse("I'm sorry, I don't have that information. I've made a note of it and will work to get it added.", false);
        }

        // add the new
        completion.messages.Insert(1, new ChatMessage("system", $"Use the following information from Samuel Ohrenberg's resume to answer any questions about him. Please note, this is only a small selection from his resume and there is likely even more that would be relevant: \r\n{relevantInfo}"));
        completion.messages.Insert(2, new ChatMessage("system", $"Please respond very concisely and professionally, as if you were in an interview. Keep your response below 100 words and keep focused. "));

        var response = await GetChatResponse(completion);

        return response;
    }

    private async Task<string> GetRelevantInformation(IEnumerable<string> tokens)
    {
        var informations = await _dbContext.Information.Include(i => i.Keywords).ToListAsync();
        foreach (var info in informations)
        {
            info.Keywords.AddRange(Tokenizer.Tokenize(info.Text).Select(t => new Keyword(t, info)));
            info.Keywords = info.Keywords.DistinctBy(k => k.Text).ToList();
        }

        //// Fetch the data from the database
        //var keywords = await _dbContext.Keywords
        //    .Include(ik => ik.Information)
        //    .Where(ik => !string.IsNullOrEmpty(ik.Information.Text))
        //    .ToListAsync();

        //// Filter the data based on the similarity percentage
        //var informations = keywords
        //    .Where(ik => tokens.Any(token => Utility.LevenshteinDifference(ik.Text, token) <= 30))
        //    .Select(ik => ik.Information)
        //    .Distinct()
        //    .ToList();


        // reduce the informations to 3 to prevent overflowing the LLM
        if (informations.Count > 3)
        {
            var countedInformations = informations.Select(i => new
            {
                Information = i,
                NumberofMatches = i.Keywords.Count(k => tokens.Any(token => Utility.LevenshteinDifference(k.Text.ToLower(), token) <= 30))
            }).ToList();
            float max = countedInformations.Max(a => a.NumberofMatches);
            float min = countedInformations.Min(a => a.NumberofMatches);
            var scoredInformation = countedInformations.Select(i => new
            {
                Information = i.Information,
                Score = ((i.NumberofMatches - min) / (max - min) * MatchWeight) + (Utility.TrueRandom(1, (100 - MatchWeight)))
            }).ToList();
            informations = scoredInformation.OrderByDescending(a => a.Score).Take(Utility.TrueRandom(2, 5)).Select(a => a.Information).ToList();
        }

        StringBuilder infoBuilder = new();

        foreach (var information in informations)
        {
            if (information.Text is not null && information.Text.Trim().Length > 0)
                infoBuilder.AppendLine(information.Text).Append("\r\n\r\n");
        }

        return infoBuilder.ToString();
    }

    private async Task CreateInformationRequest(IEnumerable<string> tokens)
    {
        var information = new Information(null, tokens);

        await _dbContext.AddAsync(information);
        await _dbContext.SaveChangesAsync();
    }
}
