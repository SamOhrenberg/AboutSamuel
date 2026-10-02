using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos;
using PortfolioWebsite.Api.Services;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class JobFitController(
    ILogger<JobFitController> logger,
    AgentServiceClient agentClient,
    SqlDbContext db) : ControllerBase
{
    // Same cap as the agent service. A long real posting is ~6-8k characters.
    private const int MaxJobDescriptionChars = 12_000;
    private const int MinJobDescriptionChars = 20;

    /// <summary>
    /// Streams a Job Fit run from the agent service straight through to the browser,
    /// and reads the events on the way past so the run can be saved when it ends.
    /// </summary>
    [HttpPost("stream")]
    [EnableRateLimiting("JobFit")]
    public async Task StreamJobFit([FromBody] JobFitRequest request, CancellationToken ct)
    {
        var jobDescription = request.JobDescription?.Trim() ?? string.Empty;
        if (jobDescription.Length < MinJobDescriptionChars || jobDescription.Length > MaxJobDescriptionChars)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new
            {
                Message = $"Paste a job description between {MinJobDescriptionChars} and {MaxJobDescriptionChars:N0} characters."
            }, ct);
            return;
        }

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var run = new JobFitRun
        {
            ReceivedAt = DateTimeOffset.UtcNow,
            JobDescription = jobDescription,
            SessionTrackingId = request.UserTrackingId,
        };
        var requirements = new Dictionary<string, JobFitRequirement>();
        var coverLetter = new StringBuilder();
        var sw = Stopwatch.StartNew();

        try
        {
            await foreach (var payload in agentClient.StreamJobFitAsync(jobDescription, ct))
            {
                await Response.WriteAsync($"data: {payload}\n\n", ct);
                await Response.Body.FlushAsync(ct);
                Capture(payload, run, requirements, coverLetter);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Visitor closed the tab. Cancelling also stopped the agent service call.
            run.Error ??= "Visitor left before the run finished.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job fit stream failed");
            run.Error ??= "Stream interrupted.";
            try
            {
                await Response.WriteAsync("data: {\"error\":\"The analysis was interrupted. Please try again.\"}\n\n", ct);
                await Response.WriteAsync("data: {\"done\":true}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            catch { /* the response is already broken, nothing more to tell the browser */ }
        }
        finally
        {
            sw.Stop();
            run.ResponseTookMs = sw.ElapsedMilliseconds;
            run.CoverLetter = coverLetter.Length > 0 ? coverLetter.ToString() : null;
            run.Requirements = requirements.Values.ToList();

            // Not the request's token: a visitor leaving shouldn't lose the log
            try
            {
                db.JobFitRuns.Add(run);
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save job fit run");
            }
        }

        logger.LogInformation(
            "Job fit run for {JobTitle} at {Company}: {Rating} ({MustHavesMet} must-haves) in {Ms} ms",
            run.JobTitle, run.Company, run.Rating, run.MustHavesMet, run.ResponseTookMs);
    }

    /// <summary>Pulls what's worth saving out of each event as it passes through.</summary>
    private void Capture(
        string payload,
        JobFitRun run,
        Dictionary<string, JobFitRequirement> requirements,
        StringBuilder coverLetter)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.TryGetProperty("token", out var token))
            {
                coverLetter.Append(token.GetString());
            }
            else if (root.TryGetProperty("requirements", out var reqs))
            {
                run.IsJobDescription = true;
                run.JobTitle = root.TryGetProperty("jobTitle", out var t) ? t.GetString() : null;
                run.Company = root.TryGetProperty("company", out var c) ? c.GetString() : null;
                foreach (var r in reqs.EnumerateArray())
                {
                    var key = r.GetProperty("id").GetString()!;
                    requirements[key] = new JobFitRequirement
                    {
                        RequirementKey = key,
                        Text = r.GetProperty("text").GetString() ?? string.Empty,
                        Category = r.GetProperty("category").GetString() ?? string.Empty,
                        Importance = r.GetProperty("importance").GetString() ?? string.Empty,
                    };
                }
            }
            else if (root.TryGetProperty("assessment", out var a))
            {
                run.Rating = a.GetProperty("rating").GetString();
                run.FitScore = a.GetProperty("fitScore").GetDouble();
                run.MustHavesMet = a.GetProperty("mustHavesMet").GetString();
                run.Summary = a.GetProperty("summary").GetRawText();
                foreach (var verdict in a.GetProperty("requirements").EnumerateArray())
                {
                    var key = verdict.GetProperty("requirementId").GetString()!;
                    if (!requirements.TryGetValue(key, out var requirement)) continue;
                    requirement.Status = verdict.GetProperty("status").GetString();
                    requirement.Reason = verdict.GetProperty("reason").GetString();
                    requirement.Citations = verdict.GetProperty("citations").GetRawText();
                }
            }
            else if (root.TryGetProperty("error", out var error))
            {
                run.Error = error.GetString();
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Never let logging break the visitor's stream
            logger.LogWarning(ex, "Couldn't read job fit event for logging: {Payload}", payload);
        }
    }
}
