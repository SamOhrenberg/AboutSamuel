using Azure;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos;
using PortfolioWebsite.Api.Services.Entities;
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
    private readonly ILogger<ChatService> _logger;
    private readonly SqlDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly ContactService _contactService;
    private readonly ModelSettings _toolUse;
    private readonly ModelSettings _questions;
    private readonly string _lmUrl;

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

    }

    internal async Task<ChatResponse> QueryChat(ChatLog chat)
    {
        DateTimeOffset receivedAt = DateTimeOffset.Now;
        Stopwatch stopwatch = new();
        stopwatch.Start();


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
                            You are a Large Language Model, or LLM.
                            You are hosted off of Sam's computer to provide an AI assistant for his portfolio. 
                            I am interfacing with you via an ASP.NET Core Web API and a Vue.js web application. 
                            You are answering on behalf of Samuel.
                            You are a professional, friendly, and helpful assistant.
                            You should identify which tool calls to make based on the user's input.
                            There can be multiple tool calls in a single response.
                            You should only use the tools that are available to you.
                            You should output a message even if a tool call is required.

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
                        response.RedirectToPage = toolCall.Arguments?["page"]?.ToString();
                        if (string.IsNullOrEmpty(response.Message))
                        {
                            response.Message = await GetPageRedirectMessage(chat.Message, response.RedirectToPage);
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
                            var questionResponse = await AskQuestion(questionCompletion, question);
                            response.Message = questionResponse.Message;
                            response.Error = response.Error || questionResponse.Error;
                            response.TokenLimitReached = response.TokenLimitReached || questionResponse.TokenLimitReached;
                        }
                        break;
                }
            }
        }
        

        // save the chat log
        stopwatch.Stop();
        _logger.LogInformation($"Chat query took {stopwatch.ElapsedMilliseconds}ms");
        await SaveChatLog(chat, response.Message, response.Error, response.TokenLimitReached, receivedAt, stopwatch.ElapsedMilliseconds);

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

        return new ChatResponse(responseText, false, Convert.ToInt32(jsonObject["usage"]["total_tokens"]) > 2500, toolCalls: toolCalls); ;
    }

    private async Task<ChatResponse> AskQuestion(dynamic completion, string? question)
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
        completion.messages.Insert(1, new ChatMessage("system", $"Use the following information from Samuel Ohrenberg's resume to answer any questions about him. Please note, this is only a small selection from his resume and there is likely even more that would be relevant: \r\n{ relevantInfo }"));
        completion.messages.Insert(2, new ChatMessage("system", $"Please respond very concisely and professionally, as if you were in an interview. Keep your response below 100 words and keep focused. "));

        var response = await GetChatResponse(completion);

        return response;
    }

    private async Task<string> GetRelevantInformation(IEnumerable<string> tokens)
    {

        var informations = await _dbContext.Keywords
            .Include(ik => ik.Information)
            .Where(ik => !string.IsNullOrEmpty(ik.Information.Text) && tokens.Contains(ik.Text))
            .Select(ik => ik.Information)
            .Distinct()
            .ToListAsync();


        // reduce the informations to 3 to prevent overflowing the LLM
        if (informations.Count > 3)
        {
            informations = Utility.Shuffle(informations).Take(Utility.TrueRandom(2,5)).ToList();
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
