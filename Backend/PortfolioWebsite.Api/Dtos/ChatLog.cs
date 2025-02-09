using PortfolioWebsite.Api.Services.Entities;
using System.Text;

namespace PortfolioWebsite.Api.Dtos;

public class ChatLog
{
    public string Message { get; set; }
    public IEnumerable<ChatMessage> History { get; set; }
    public Guid? UserTrackingId { get; set; }

    public override string ToString()
    {
        StringBuilder output = new StringBuilder();
        foreach (var history in History)
        {
            output.AppendLine(history.ToString());
        }
        output.AppendFormat("User: {0}", Message);
        return output.ToString();
    }

    public string PrintHistory()
    {
        StringBuilder output = new StringBuilder();
        foreach (var history in History)
        {
            output.AppendLine(history.ToString());
        }
        return output.ToString();
    }
}
