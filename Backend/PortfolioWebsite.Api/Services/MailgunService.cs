using RestSharp;

namespace PortfolioWebsite.Api.Services;

public class MailgunService
{
    private readonly string? _apiKey;
    private readonly string? _domain;
    private readonly string? _fromEmail;
    private readonly string? _bccEmail;

    public MailgunService(IConfiguration configuration)
    {
        var settings = configuration.GetSection("MailgunSettings");
        _apiKey = settings["ApiKey"];
        _domain = settings["Domain"];
        _fromEmail = settings["From"];
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var client = new RestClient($"https://api.mailgun.net/v3/{_domain}");
        var request = new RestRequest("messages", Method.Post)
            .AddHeader("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"api:{_apiKey}")))
            .AddParameter("from", _fromEmail)
            .AddParameter("to", toEmail)
            .AddParameter("cc", _fromEmail)
            .AddParameter("bcc", _bccEmail)
            .AddParameter("subject", subject)
            .AddParameter("html", body);

        var response = await client.ExecuteAsync(request);

        if (!response.IsSuccessful)
        {
            throw new Exception($"Mailgun API Error: {response.Content}");
        }
    }
}
