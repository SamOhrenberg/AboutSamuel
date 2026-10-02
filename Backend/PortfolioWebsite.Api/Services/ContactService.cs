using System.Text;

namespace PortfolioWebsite.Api.Services;

public class ContactService
{
    private readonly string _toEmail;
    private readonly AzureEmailService _email;

    public ContactService(IConfiguration configuration, AzureEmailService emailService)
    {
        _toEmail = configuration.GetValue<string>("EmailSettings:To")
            ?? throw new InvalidOperationException("EmailSettings:To must be configured.");
        _email = emailService;
    }

    public async Task SendContactRequest(string email, string? message)
    {
        StringBuilder messageBuilder = new StringBuilder("You have received a contact request from.");

        messageBuilder.AppendFormat("<br/>Email: {0}", email);

        if (!string.IsNullOrEmpty(message))
        {
            messageBuilder.AppendFormat("<br/>Message: {0}", message);
        }

        // Reply-To the visitor so hitting Reply goes to them, not DoNotReply@
        await _email.SendEmailAsync(_toEmail, $"Contact Request for {email}", messageBuilder.ToString(), replyTo: email);
    }
}
