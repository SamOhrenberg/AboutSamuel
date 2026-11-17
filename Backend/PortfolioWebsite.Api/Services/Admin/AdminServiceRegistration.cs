namespace PortfolioWebsite.Api.Services.Admin;

public static class AdminServiceRegistration
{
    /// <summary>The admin operations shared by the admin controllers and the admin MCP tools.</summary>
    public static IServiceCollection AddAdminServices(this IServiceCollection services)
    {
        services.AddScoped<AdminProjectService>();
        services.AddScoped<AdminWorkExperienceService>();
        services.AddScoped<AdminInformationService>();
        services.AddScoped<AdminEmbeddingService>();
        services.AddScoped<AdminRecruiterService>();
        services.AddScoped<AdminResumeService>();
        services.AddScoped<AdminActivityService>();
        return services;
    }
}
