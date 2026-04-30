using Amazon.BedrockRuntime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Middlewares;
using PortfolioWebsite.Api.Services;
using Serilog;
using Serilog.Events;
using System.Text;
using System.Threading.RateLimiting;

namespace PortfolioWebsite.Api
{
    public class Program
    {
        const string PublicCorsPolicy = "ProductionCors";
        const string AdminCorsPolicy = "AdminCors";

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateBootstrapLogger();

            try
            {
                Log.Information("Starting PortfolioWebsite.Api host...");

                AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);

                var builder = WebApplication.CreateBuilder(args);

                builder.Host.UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext());

                builder.Services.AddControllers();
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen();

                builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
                builder.Services.AddSingleton<IAmazonBedrockRuntime>(_ =>
                {
                    var regionStr = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
                    Log.Information("Initializing AWS Bedrock Runtime in region: {Region}", regionStr);
                    var region = Amazon.RegionEndpoint.GetBySystemName(regionStr);
                    return new AmazonBedrockRuntimeClient(region);
                });

                builder.Services.AddScoped<EmbeddingService>();

                var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
                Log.Information("CORS allowed origins: {Origins}", string.Join(", ", allowedOrigins ?? ["None"]));

                builder.Services.AddCors(options =>
                {
                    options.AddPolicy(PublicCorsPolicy, policy => policy
                        .WithOrigins(allowedOrigins ?? Array.Empty<string>())
                        .AllowAnyMethod()
                        .AllowAnyHeader());

                    options.AddPolicy(AdminCorsPolicy, policy => policy
                        .WithOrigins(allowedOrigins ?? Array.Empty<string>())
                        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                        .AllowAnyHeader());
                });

                var jwtSecret = builder.Configuration.GetValue<string>("AdminSettings:JwtSecret")
                    ?? throw new InvalidOperationException("AdminSettings:JwtSecret must be configured.");

                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                            ValidateIssuer = true,
                            ValidIssuer = "aboutsamuel.com",
                            ValidateAudience = true,
                            ValidAudience = "aboutsamuel-admin",
                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.Zero
                        };
                    });

                builder.Services.AddAuthorization();

                builder.Services.AddRateLimiter(options =>
                {
                    options.AddFixedWindowLimiter("AdminLogin", limiterOptions =>
                    {
                        limiterOptions.Window = TimeSpan.FromHours(1);
                        limiterOptions.PermitLimit = 5;
                        limiterOptions.QueueLimit = 0;
                        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    });
                    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                });

                builder.Services.AddPooledDbContextFactory<SqlDbContext>(options =>
                    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

                builder.Services.AddScoped(sp =>
                    sp.GetRequiredService<IDbContextFactory<SqlDbContext>>().CreateDbContext());

                builder.Services.AddScoped<ChatService>();
                builder.Services.AddScoped<ContactService>();
                builder.Services.AddScoped<AdminService>();
                builder.Services.AddSingleton<MailgunService>();

                var app = builder.Build();

                app.UseSerilogRequestLogging();

                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }

                app.UseMiddleware<ExceptionLoggerMiddleware>();
                app.UseExceptionHandler("/error");
                app.UseHttpsRedirection();
                app.UseRateLimiter();

                app.UseCors(PublicCorsPolicy);
                app.UseAuthentication();
                app.UseAuthorization();

                app.MapControllers();

                using (var scope = app.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<SqlDbContext>();
                    var pendingMigrations = db.Database.GetPendingMigrations().ToList();

                    if (pendingMigrations.Any())
                    {
                        Log.Information("Applying {Count} pending migrations: {Migrations}",
                            pendingMigrations.Count, string.Join(", ", pendingMigrations));
                        db.Database.Migrate();
                        Log.Information("Migrations applied successfully.");
                    }
                    else
                    {
                        Log.Information("No pending migrations found.");
                    }
                }

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly during startup");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}