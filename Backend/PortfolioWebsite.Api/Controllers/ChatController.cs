using Microsoft.AspNetCore.Mvc;
using PortfolioWebsite.Api.Data;
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
            RedirectToPage = chatResponse.RedirectToPage
        };
    }

    [HttpPost("stream")]
    public async Task StreamChat(
    [FromBody] ChatLog chat,
    CancellationToken ct,
    [FromServices] AgentServiceClient agentClient,
    [FromServices] ILogger<ChatController> logger,
    [FromServices] SqlDbContext db)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var receivedAt = DateTimeOffset.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var fullResponse = new System.Text.StringBuilder();
        bool error = false;
        bool tokenLimitReached = false;
        string? redirectToPage = null;

        await foreach (var chunk in agentClient.StreamChatAsync(chat, ct))
        {
            if (chunk.IsToken)
            {
                fullResponse.Append(chunk.Token);
                var payload = System.Text.Json.JsonSerializer.Serialize(
                    new { token = chunk.Token });
                await Response.WriteAsync($"data: {payload}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            else if (chunk.IsMeta)
            {
                error = chunk.Meta!.Error;
                redirectToPage = chunk.Meta.RedirectToPage;

                // If the meta has the full response from Python, use that for logging
                if (!string.IsNullOrEmpty(chunk.Meta.FullResponse))
                    fullResponse.Clear().Append(chunk.Meta.FullResponse);

                var metaPayload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    redirectToPage,
                    tokenLimitReached,
                    error
                });
                await Response.WriteAsync($"data: {metaPayload}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        
        await Response.WriteAsync("data: [DONE]\n\n", ct);
        await Response.Body.FlushAsync(ct);

        sw.Stop();

        // Save chat log to PostgreSQL via C# as before
        try
        {
            db.Chats.Add(new PortfolioWebsite.Api.Data.Models.Chat
            {
                History = chat.PrintHistory(),
                Message = chat.Message,
                Response = fullResponse.ToString(),
                ReceivedAt = receivedAt,
                ResponseTookMs = sw.ElapsedMilliseconds,
                Error = error,
                TokenLimitReached = tokenLimitReached,
                SessionTrackingId = chat.UserTrackingId
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save chat log");
        }
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