using Microsoft.AspNetCore.Mvc;
using PortfolioWebsite.Api.Services;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("embedding-visualization")]
public class EmbeddingVisualizationController(EmbeddingProjectionService _projectionService, ILogger<EmbeddingVisualizationController> _logger) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> GetProjections()
    {
        var points = await _projectionService.GetProjectionsAsync();

        if (points.Count == 0)
            return Ok(new { points = new List<object>(), message = "No projections yet. Regenerate from the admin panel." });

        return Ok(new { points });
    }

    public record QueryRequest(string Query, int TopN = 5);


    [HttpPost("query")]
    public async Task<IActionResult> QueryEmbedding([FromBody] QueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { Message = "Query text is required." });

        var topN = Math.Clamp(request.TopN, 1, 10);

        try
        {
            var result = await _projectionService.ProjectQueryAsync(request.Query, topN);

            if (result == null)
                return StatusCode(503, new { Message = "Embedding service unavailable." });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error projecting query: {Query}", request.Query);
            return StatusCode(500, new { Message = "Query projection failed." });
        }
    }
}
