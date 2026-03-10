using Microsoft.AspNetCore.Mvc;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController() : ControllerBase
{
    [HttpGet, HttpHead]
    public async Task<IActionResult> Get() => Ok(new { Status = "Healthy" });
}
