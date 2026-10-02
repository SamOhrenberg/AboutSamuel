using Azure;
using Azure.Communication.Email;

namespace PortfolioWebsite.Api.Services;

/// <summary>
/// Sends email through Azure Communication Services. Replaced Mailgun, which
/// suspended the account for inactivity.
/// </summary>
public class AzureEmailService
{
    private readonly EmailClient _client;
    private readonly string _fromEmail;

    public AzureEmailService(IConfiguration configuration)
    {
        var settings = configuration.GetSection("EmailSettings");

        var connectionString = settings["ConnectionString"]
            ?? throw new InvalidOperationException("EmailSettings:ConnectionString must be configured.");
        _fromEmail = settings["From"]
            ?? throw new InvalidOperationException("EmailSettings:From must be configured.");

        _client = new EmailClient(connectionString);
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, string? replyTo = null)
    {
        var message = new EmailMessage(
            senderAddress: _fromEmail,
            recipientAddress: toEmail,
            content: new EmailContent(subject) { Html = body });

        if (!string.IsNullOrWhiteSpace(replyTo))
            message.ReplyTo.Add(new EmailAddress(replyTo));

        // Started returns once Azure accepts the message instead of polling until
        // delivery, which keeps the agent service's contact tool under its timeout.
        // Bad sender or unverified domain still throws here.
        await _client.SendAsync(WaitUntil.Started, message);
    }
}
