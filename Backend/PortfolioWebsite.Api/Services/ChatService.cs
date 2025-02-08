using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Common;
using System;
using System.Text;
using static PortfolioWebsite.Api.Controllers.ChatController;

namespace PortfolioWebsite.Api.Services;

public class ChatService
{
    private readonly ILogger<ChatService> _chatService;
    private readonly SqlDbContext _dbContext;

    public ChatService(ILogger<ChatService> chatService, SqlDbContext dbContext)
    {
        _chatService = chatService;
        _dbContext = dbContext;
    }

    public record ChatResponse(string Message, bool TokenLimitReached, bool Error);
    internal async Task<ChatResponse> QueryChat(ChatLog chat)
    {
        var fullUserChatLog = string.Concat(chat.Message, chat.History.Where(h => h.Role.Equals("user", StringComparison.OrdinalIgnoreCase)).Select(h => h.Content));
        var tokens = Tokenizer.Tokenize(fullUserChatLog);
        var relevantInfo = await GetRelevantInformation(tokens);

        if (relevantInfo is null || relevantInfo.Length == 0)
        {
            await CreateInformationRequest(tokens);
            return new ChatResponse("I'm sorry, I don't have that information. I've made a note of it and will work to get it added.", false, true);
        }


    }

    private async Task<string> GetRelevantInformation(IEnumerable<string> tokens)
    {

        var informationKeywords = await _dbContext.InformationKeyword
            .Include(ik => ik.Keyword)
            .Include(ik => ik.Information)
            .Where(ik => tokens.Contains(ik.Keyword.Text))
            .ToListAsync();

        StringBuilder infoBuilder = new();

        foreach (var informationKeyword in informationKeywords)
        {
            if (informationKeyword.Information is not null && informationKeyword.Information.Text.Trim().Length > 0)
                infoBuilder.AppendLine(informationKeyword.Information.Text);
        }

        return infoBuilder.ToString();
    }

    private async Task CreateInformationRequest(IEnumerable<string> tokens)
    {
        var information = new Information
        {
            InformationId = Guid.NewGuid(),
            Keywords = tokens.Select(k => new Keyword { Text = k }).ToList()
        };

        await _dbContext.AddAsync(information);
        await _dbContext.SaveChangesAsync();
    }
}
