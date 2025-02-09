using Azure;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Common;
using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using static PortfolioWebsite.Api.Controllers.ChatController;

namespace PortfolioWebsite.Api.Services;

public class ChatService
{
    private readonly ILogger<ChatService> _chatService;
    private readonly SqlDbContext _dbContext;
    private readonly HttpClient _httpClient;

    public ChatService(ILogger<ChatService> chatService, SqlDbContext dbContext, HttpClient httpClient)
    {
        _chatService = chatService;
        _dbContext = dbContext;
        _httpClient = httpClient;
    }

    public record ChatResponse(string Message, bool TokenLimitReached, bool Error);
    internal async Task<ChatResponse> QueryChat(ChatLog chat)
    {
        var fullUserChatLog = string.Concat(chat.Message);
        foreach (var entry in chat.History)
        {
            if (entry.Role.Equals("user", StringComparison.OrdinalIgnoreCase) && entry.Content.Length > 0)
                fullUserChatLog += entry.Content;
        }
        var tokens = Tokenizer.Tokenize(fullUserChatLog);
        var relevantInfo = await GetRelevantInformation(tokens);

        if (relevantInfo is null || relevantInfo.Length == 0)
        {
            await CreateInformationRequest(tokens);
            return new ChatResponse("I'm sorry, I don't have that information. I've made a note of it and will work to get it added.", false, true);
        }


        string model = "mistral-nemo-instruct-2407";
        var completion = new
        {
            model,
            temperature = 0.5,
            messages = new List<ChatHistory>
                {
                    new ChatHistory("system", $"""
                            
                            Context:  
                                You are an AI chatbot. Your name is SamuelLM.                         
                                You were created by Samuel Ohrenberg, who also goes by Sam or Sammy.
                                You are a Large Language Model, or LLM, using { model }.
                                You are hosted off of Sam's computer to provide an AI assistant for his portfolio. 
                                I am interfacing with you via an ASP.NET Core Web API and a Vue.js web application. 
                            
                            Instructions:
                                You should field any questions that I have about Sam. 
                                Make Sam sound knowledgeable and down to earth.
                                If I asks you something you don't know about him, then just tell me but emphasize that Sam will get it added to your model.
                                If I ask about any non-professional, personal details about anybody, including Sammy or his family, then kindly decline to answer and refer them back to the fact that this page is about his professional, software engineering career. 
                                Output less than one paragraph or 100 words
                                Tell me to ask for more details if there is more than one paragraph worth of information to take about.

                            Use the following information from my resume to answer questions about Samuel Ohrenberg:

                            { relevantInfo }

                            For now, the word "you" is referring to Samuel Ohrenberg. Any questions about you should be answered as if you were Samuel Ohrenberg using the information mentioned before.

                        """
                    )
                }
        };

        foreach (var entry in chat.History)
        {
            completion.messages.Add(entry);
        }
        completion.messages.Add(new ChatHistory("user", chat.Message));

        // request the chat
        var response = await _httpClient.PostAsJsonAsync("http://127.0.0.1:1234/v1/chat/completions", completion);
        var responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return new ChatResponse(responseText, false, true);
        }

        JObject jsonObject = JObject.Parse(responseText);
        var text = Regex.Replace((jsonObject["choices"] as JArray).First()["message"]["content"].ToString().Replace("*", ""), @"\s+", " ");

        if (text.Contains("</think>"))
        {
            text = text.Split("</think>")[1];
        }

        return new ChatResponse(text, Convert.ToInt32(jsonObject["usage"]["total_tokens"]) > 2500, false);
    }

    private async Task<string> GetRelevantInformation(IEnumerable<string> tokens)
    {

        var informationKeywords = await _dbContext.Keywords
            .Include(ik => ik.Information)
            .Where(ik => tokens.Contains(ik.Text))
            .ToListAsync();

        StringBuilder infoBuilder = new();

        foreach (var informationKeyword in informationKeywords)
        {
            if (informationKeyword.Information?.Text is not null && informationKeyword.Information.Text.Trim().Length > 0)
                infoBuilder.AppendLine(informationKeyword.Information.Text);
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
