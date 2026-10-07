using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using PortfolioWebsite.Api.Services;

namespace PortfolioWebsite.Api.Controllers;

/// <summary>
/// Called by the agent service's recruiter triage when postings need Samuel's review.
/// Everything in the request came from recruiter emails, so it's all HTML-encoded.
/// </summary>
[ApiController]
[Route("recruiters/internal")]
public class RecruiterNotifyController(
    ILogger<RecruiterNotifyController> logger, AzureEmailService email, IConfiguration config) : ControllerBase
{
    public record NotifyPosting(
        string? JobTitle, string? Company, string? Location, string? WorkArrangement, double? AnnualPay,
        List<string> Agencies, List<string> Reasons, string GmailUrl);

    public record NotifyRequest(List<NotifyPosting> Postings);

    [HttpPost("notify")]
    public async Task<IActionResult> Notify(
        [FromBody] NotifyRequest request,
        [FromHeader(Name = "X-Internal-Secret")] string? secret)
    {
        var expectedSecret = config["AgentService:InternalSecret"];
        if (string.IsNullOrEmpty(expectedSecret) || secret != expectedSecret)
            return Unauthorized();
        if (request.Postings.Count == 0)
            return Ok();

        var html = new StringBuilder("<p>New recruiter pitches that look like a match:</p>");
        foreach (var p in request.Postings.Take(20))
        {
            var title = Enc(p.JobTitle ?? "Untitled role");
            var where = string.Join(", ", new[] { p.Company, p.Location, p.WorkArrangement }
                .Where(s => !string.IsNullOrWhiteSpace(s) && s != "unknown").Select(Enc));
            var pay = p.AnnualPay is { } a ? $"${a:N0}/yr" : "pay not stated";

            html.Append($"<h3 style=\"margin-bottom:4px\">{title}</h3>");
            html.Append($"<div>{where}{(where.Length > 0 ? " &middot; " : "")}{pay}</div>");
            html.Append($"<div>From: {string.Join(", ", p.Agencies.Take(10).Select(Enc))}</div>");
            if (p.Reasons.Count > 0)
                html.Append($"<div>Notes: {string.Join("; ", p.Reasons.Take(5).Select(Enc))}</div>");
            // Only link to Gmail itself, never to a URL supplied by an email
            if (p.GmailUrl.StartsWith("https://mail.google.com/"))
                html.Append($"<div><a href=\"{Enc(p.GmailUrl)}\">Open in Gmail</a></div>");
        }
        html.Append("<p>A reply draft is waiting in each thread. Review queue: " +
                    "<a href=\"https://aboutsamuel.com/admin\">aboutsamuel.com/admin</a> (Recruiters tab).</p>");

        var to = config["EmailSettings:To"];
        if (string.IsNullOrEmpty(to))
            return StatusCode(500, new { Message = "EmailSettings:To is not configured." });

        try
        {
            var count = request.Postings.Count;
            await email.SendEmailAsync(to, $"Recruiter triage: {count} possible match{(count == 1 ? "" : "es")}",
                html.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Recruiter notification failed");
            return StatusCode(500, new { Message = "Internal server error." });
        }
        return Ok();
    }

    private static string Enc(string? s) => WebUtility.HtmlEncode(s ?? "");
}
