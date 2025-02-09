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

namespace PortfolioWebsite.Api.Services;

public class ChatService
{
    private readonly ILogger<ChatService> _logger;
    private readonly SqlDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly ContactService _contactService;
    private readonly string _model;
    private readonly float _temperature;
    private readonly string _lmUrl;

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, SqlDbContext dbContext, HttpClient httpClient, ContactService contactService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _httpClient = httpClient;
        _contactService = contactService;
        _model = configuration.GetValue<string>("ChatSettings:Model") ?? throw new NullReferenceException("ChatSettings:Model must be populated in settings");
        _temperature = configuration.GetValue<float?>("ChatSettings:Temperature") ?? throw new NullReferenceException("ChatSettings:Temperature must be populated in settings");
        _lmUrl = configuration.GetValue<string>("ChatSettings:Url") ?? throw new NullReferenceException("ChatSettings:Url must be populated in settings");
    }

    internal async Task<ChatResponse> QueryChat(ChatLog chat)
    {
        DateTimeOffset receivedAt = DateTimeOffset.Now;
        Stopwatch stopwatch = new();
        stopwatch.Start();


        var completion = new
        {
            model = _model,
            temperature = _temperature,
            messages = new List<ChatMessage>
            {
                new ChatMessage("system", $"""
                            
                        Context:  
                            You are an AI chatbot. Your name is SamuelLM.                         
                            You were created by Samuel Ohrenberg, who also goes by Sam or Sammy.
                            You are a Large Language Model, or LLM, using { _model }.
                            You are hosted off of Sam's computer to provide an AI assistant for his portfolio. 
                            I am interfacing with you via an ASP.NET Core Web API and a Vue.js web application. 
                            
                        For now, the word "you" is referring to Samuel Ohrenberg. Any questions about you should be answered as if you were Samuel Ohrenberg.

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

        var toolCallsMatch = Regex.Match(response.Message, @"\[TOOL_CALLS\](\[.*?\])(.*)");
        if (toolCallsMatch.Success)
        {
            string toolCallsJson = toolCallsMatch.Groups[1].Value;
            var toolCalls = JArray.Parse(toolCallsJson);

            foreach (var toolCall in toolCalls)
            {
                switch (toolCall["name"]?.ToString())
                {
                    case "contactSamuel":
                        var email = toolCall["arguments"]?["email"]?.ToString();
                        var msg = toolCall["arguments"]?["message"]?.ToString();
                        if (!string.IsNullOrEmpty(email))
                        {
                            await _contactService.SendContactRequest(email, msg);
                        }
                        response.Message = toolCallsMatch.Groups[2].Value;
                        break;
                    case "getResume":
                        response.ReturnResume = true;
                        response.Message = "Of course. Here is Sam's resume!";
                        break;
                    case "redirectToPage":
                        response.RedirectToPage = toolCall["arguments"]?["page"]?.ToString();
                        response.Message = toolCallsMatch.Groups[2].Value;
                        break;
                    case "askQuestion":
                        string? question = toolCall["arguments"]?["question"]?.ToString();
                        if (!string.IsNullOrEmpty(question))
                        {
                            var questionCompletion = new
                            {
                                completion.model,
                                completion.messages,
                                completion.temperature
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
        responseText = Regex.Replace((jsonObject["choices"] as JArray).First()["message"]["content"].ToString().Replace("*", ""), @"\s+", " ");
        if (responseText.Contains("</think>"))
        {
            responseText = responseText.Split("</think>")[1];
        }

        return new ChatResponse(responseText, false, Convert.ToInt32(jsonObject["usage"]["total_tokens"]) > 2500);
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
