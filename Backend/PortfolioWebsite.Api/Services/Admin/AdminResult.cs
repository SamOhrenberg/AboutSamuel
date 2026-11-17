using Microsoft.AspNetCore.Mvc;

namespace PortfolioWebsite.Api.Services.Admin;

public enum AdminStatus { Ok, NotFound, Invalid, Conflict, Unavailable, Failed }

/// <summary>Why an admin operation didn't happen. Converts to any AdminResult.</summary>
public sealed record AdminFailure(AdminStatus Status, string? Message)
{
    public static AdminFailure NotFound(string? message = null) => new(AdminStatus.NotFound, message);
    public static AdminFailure Invalid(string message) => new(AdminStatus.Invalid, message);
    public static AdminFailure Conflict(string message) => new(AdminStatus.Conflict, message);
    /// <summary>Another service (the agent service, Azure OpenAI) didn't do its part.</summary>
    public static AdminFailure Unavailable(string message) => new(AdminStatus.Unavailable, message);
    /// <summary>Something broke here. The details are in the logs.</summary>
    public static AdminFailure Failed(string message) => new(AdminStatus.Failed, message);
}

/// <summary>
/// What an admin operation did. The admin controllers turn it into HTTP responses and the
/// admin MCP tools turn it into tool results, so validation and messages live in one place.
/// </summary>
public record AdminResult(AdminStatus Status, string? Message = null)
{
    public bool IsOk => Status == AdminStatus.Ok;

    public static AdminResult Ok() => new(AdminStatus.Ok);

    public static implicit operator AdminResult(AdminFailure failure) => new(failure.Status, failure.Message);

    /// <summary>The failure as the admin API has always returned it.</summary>
    public IActionResult ToFailure() => Status switch
    {
        AdminStatus.NotFound => Message is null ? new NotFoundResult() : new NotFoundObjectResult(new { Message }),
        AdminStatus.Invalid => new BadRequestObjectResult(new { Message }),
        AdminStatus.Conflict => new ConflictObjectResult(new { Message }),
        AdminStatus.Unavailable => new ObjectResult(new { Message }) { StatusCode = StatusCodes.Status502BadGateway },
        AdminStatus.Failed => new ObjectResult(new { Message }) { StatusCode = StatusCodes.Status500InternalServerError },
        _ => throw new InvalidOperationException("A successful result isn't a failure."),
    };

    /// <summary>Success as the given response, or the failure.</summary>
    public IActionResult ToActionResult(Func<IActionResult> ok) => IsOk ? ok() : ToFailure();
}

public sealed record AdminResult<T>(AdminStatus Status, T? Value, string? Message = null) : AdminResult(Status, Message)
{
    public static implicit operator AdminResult<T>(T value) => new(AdminStatus.Ok, value);
    public static implicit operator AdminResult<T>(AdminFailure failure) => new(failure.Status, default, failure.Message);

    /// <summary>The value as an HTTP response, or the failure.</summary>
    public IActionResult ToActionResult(Func<T, IActionResult> ok) => IsOk ? ok(Value!) : ToFailure();
}
