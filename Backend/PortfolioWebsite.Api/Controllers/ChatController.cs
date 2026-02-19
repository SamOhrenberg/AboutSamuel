using Microsoft.AspNetCore.Mvc;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos;
using PortfolioWebsite.Api.Services;
using PortfolioWebsite.Api.Services.Entities;
using PortfolioWebsite.Common;
using System.Data;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ChatController(ILogger<ChatController> _logger, ChatService _chatService) : ControllerBase
{
    [HttpPost]
    public async Task<SamuelLMResponse> Post(ChatLog chat)
    {
        _logger.LogInformation("POST /api/chat from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
        _logger.LogDebug("Payload: {chat}", JsonSerializer.Serialize(chat));
        var chatResponse = await _chatService.QueryChat(chat);

        if (chatResponse.TokenLimitReached)
        {
            Response.Headers.Append("X-Token-Limit-Reached", "true");
        }

        if (chatResponse.Error)
        {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }

        return new SamuelLMResponse
        {
            Message = chatResponse.Message,
            DisplayResume = chatResponse.ReturnResume,
            RedirectToPage = chatResponse.RedirectToPage
        };
    }

    [HttpGet("resume/{jobTitle?}")]
    public async Task<string?> GetResume(string? jobTitle)
    {
        _logger.LogInformation("POST /api/chat/resume/{jobTitle} from {RemoteIp}", jobTitle ?? "", HttpContext.Connection.RemoteIpAddress);
        string? html = await _chatService.GenerateHtmlResume(jobTitle);
        return html;
    }
}