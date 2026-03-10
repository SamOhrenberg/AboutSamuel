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

    [HttpPost("stream")]
    public async Task StreamChat([FromBody] ChatLog chat, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        await foreach (var chunk in _chatService.StreamChat(chat, ct))
        {
            string payload;

            if (chunk.IsToken)
                payload = JsonSerializer.Serialize(new { token = chunk.Token });
            else
                payload = JsonSerializer.Serialize(chunk.Meta); // { redirectToPage, displayResume, tokenLimitReached }

            await Response.WriteAsync($"data: {payload}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        await Response.WriteAsync("data: [DONE]\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }


    [HttpGet("resume")]
    public async Task<IActionResult> GetResume()
    {
        var data = await _chatService.GenerateResumeData(null, null);
        return data is null ? Problem() : Ok(data);
    }

    [HttpPost("resume")]
    public async Task<IActionResult> GetTailoredResume([FromBody] GenerateResumeRequest request)
    {
        var data = await _chatService.GenerateResumeData(request.Title, request.JobDescription);
        return data is null ? Problem() : Ok(data);
    }

}