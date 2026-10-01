using Microsoft.AspNetCore.Mvc;
using PortfolioWebsite.Api.Dtos;
using PortfolioWebsite.Api.Services;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ContactController(ILogger<ContactController> logger, ContactService contactService) : ControllerBase
{
    [HttpPost("internal")]
    public async Task<IActionResult> PostInternal(
        [FromBody] ContactRequest contactRequest,
        [FromHeader(Name = "X-Internal-Secret")] string? secret,
        [FromServices] IConfiguration config)
    {
        var expectedSecret = config["AgentService:InternalSecret"];
        if (string.IsNullOrEmpty(expectedSecret) || secret != expectedSecret)
            return Unauthorized();

        try
        {
            await contactService.SendContactRequest(contactRequest.Email, contactRequest.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Internal contact failure for {Email}", contactRequest.Email);
            return StatusCode(500, new { Message = "Internal server error." });
        }

        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> Post(ContactRequest contactRequest)
    {
        try
        {
            await contactService.SendContactRequest(contactRequest.Email, contactRequest.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Contact form failure for {Email}", contactRequest.Email);
            return StatusCode(500, new { Message = "Internal server error. Please try again later." });
        }

        return Ok();
    }
}