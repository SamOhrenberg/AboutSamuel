using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using PortfolioWebsite.Api.Services;
using PortfolioWebsite.Common;
using System.Data;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PortfolioWebsite.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ILogger<ChatController> _logger;
        private readonly ChatService _chatService;

        public record ChatHistory(string Role, string Content);
        public record ChatLog(string Message, IEnumerable<ChatHistory> History);

        public ChatController(ILogger<ChatController> logger, ChatService chatService)
        {
            _logger = logger;
            _chatService = chatService;
        }

        [HttpPost]
        public async Task<string> Post(ChatLog chat)
        {
            var chatResponse = await _chatService.QueryChat(chat);

            if (chatResponse.TokenLimitReached)
            {
                Response.Headers.Append("X-Token-Limit-Reached", "true");
            }

            if (chatResponse.Error)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }

            return chatResponse.Message;
        }
    }
}
