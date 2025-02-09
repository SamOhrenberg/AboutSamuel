﻿using Newtonsoft.Json;

namespace PortfolioWebsite.Api.Services.Entities
{
    public class ChatMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
