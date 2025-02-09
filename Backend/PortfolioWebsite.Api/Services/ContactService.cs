using System.Text;

namespace PortfolioWebsite.Api.Services;

public class ContactService
{
    private readonly string? _toEmail;
    private readonly MailgunService _mailgun;

    public ContactService(IConfiguration configuration, MailgunService mailgunService)
    {
        _toEmail = configuration.GetValue<string>("MailgunSettings:To");
        _mailgun = mailgunService;
    }

    public async Task SendContactRequest(string email, string? message)
    {
        StringBuilder messageBuilder = new StringBuilder("You have received a contact request from.");

        messageBuilder.AppendFormat("<br/>Email: {0}", email);

        if (!string.IsNullOrEmpty(message))
        {
            messageBuilder.AppendFormat("<br/>Message: {0}", message);
        }

        await _mailgun.SendEmailAsync(_toEmail, $"Contact Request for {email}", messageBuilder.ToString());
    }
}
