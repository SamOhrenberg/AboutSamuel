using Azure.AI.OpenAI;
using Azure.Identity;

namespace PortfolioWebsite.Api.Services;

/// <summary>
/// The only place the Azure OpenAI client is built. Auth is Entra ID through
/// DefaultAzureCredential, which needs Cognitive Services OpenAI User on the Foundry account
/// (infra/bootstrap). The client asks for the https://cognitiveservices.azure.com/.default
/// scope itself. Which credential answers is pinned by the AZURE_TOKEN_CREDENTIALS env var:
/// EnvironmentCredential on Railway (the sp-aboutsamuel-railway service principal), unset
/// locally. Locally the chain tries Visual Studio before the Azure CLI, so if VS is signed in
/// as someone else, set AZURE_TOKEN_CREDENTIALS=AzureCliCredential.
///
/// There's no key fallback: the Foundry account has disableLocalAuth on, so keys get a 403.
/// </summary>
public static class AzureOpenAIRegistration
{
    public static IServiceCollection AddAzureOpenAI(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(AzureOpenAIRegistration));

            var endpoint = new Uri(configuration["AzureOpenAI:Endpoint"]
                ?? throw new InvalidOperationException("AzureOpenAI:Endpoint must be configured."));

            logger.LogInformation("Azure OpenAI auth: {AuthMode}", "entra_id");
            return new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
        });
        return services;
    }
}
