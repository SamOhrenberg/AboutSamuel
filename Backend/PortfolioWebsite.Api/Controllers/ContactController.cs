using Microsoft.AspNetCore.Mvc;
using PortfolioWebsite.Api.Dtos;
using PortfolioWebsite.Api.Services;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ContactController(ILogger<ContactController> logger, ContactService contactService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post(ContactRequest contactRequest)
    {
        try
        {
            await contactService.SendContactRequest(contactRequest.Email, contactRequest.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ex.Message
            });
        }

        return Ok();
    }
}