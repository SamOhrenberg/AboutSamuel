using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Text.Json;

namespace PortfolioWebsite.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ILogger<ChatController> _logger;
        private readonly HttpClient _httpClient;

        public ChatController(ILogger<ChatController> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        public record ChatLog(string Message, IEnumerable<string> History);

        [HttpPost]
        public async Task<string> Post(ChatLog chat)
        {
            var completion = new
            {
                model = "deepseek-r1-distill-qwen-7b",
                temperature = 0,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = """
                            You are an AI chatbot for me, Samuel Ohrenberg, running mistral on a local LLM via LM Studio. You operate as an assistant on my 
                            personal portfolio website as a testament to my knowledge in programming. The user is interfacing with you via an ASP.NET Core Web API. You
                            should field any questions that the user has about me. You should make me sound great while also making me sound down to earth. If the user
                            asks you something you don't know about me, then just tell them but emphasize that we will get it added.
                            If the user asks about any non-professional, personal details about me, then kindly decline to answer and refer them back to the fact that this page
                            is about my professional, software engineering career. 

                            When answering questions about job or project history, only state job history mentioned in this prompt. If no job history or projects are mentioned, tell the user you do not have that information.
                            Only use data mentioned in this specific prompt to answer questions. Be very concise in your response.

                            The best project I have ever worked on is an application called PHIDDO for the Oklahoma State Department of Health that uses an OpenSilver web application in VB.NET to enable to tracking of viral and contageous diseases across the state for reporting to the CDC.
                        """
                    },
                    new {
                        role = "user",
                        content = chat.Message
                    }
                }
            };
            var response = await _httpClient.PostAsJsonAsync("http://127.0.0.1:1234/v1/chat/completions", completion);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(await response.Content.ReadAsStringAsync());
            }
            var responseText = await response.Content.ReadAsStringAsync();
            JObject jsonObject = JObject.Parse(responseText);
            return (jsonObject["choices"] as JArray).First()["message"]["content"].ToString();
        }
    }
}
