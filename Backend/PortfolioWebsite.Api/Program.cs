using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Middlewares;
using PortfolioWebsite.Api.Services;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Elasticsearch;
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

                builder.Host.UseSerilog((context, services, configuration) =>
                {
                    var axiomToken = context.Configuration["Axiom:Token"];
                    var axiomDataset = context.Configuration["Axiom:Dataset"];

                    configuration
                        .ReadFrom.Configuration(context.Configuration)
                        .WriteTo.Http(
                            requestUri: $"https://api.axiom.co/v1/datasets/{axiomDataset}/ingest",
                            queueLimitBytes: null,
                            httpClient: new AxiomHttpService(axiomToken!),
                            textFormatter: new ElasticsearchJsonFormatter(
                                renderMessageTemplate: false, inlineFields: true)
                        )
                        .Enrich.FromLogContext();
                });

                builder.Services.AddControllers();
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen();

                builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
                builder.Services.AddSingleton<ILlmService, AzureLlmService>();
                builder.Services.AddScoped<EmbeddingService>(sp =>
                    (EmbeddingService)sp.GetRequiredService<IEmbeddingService>());

                builder.Services.AddScoped<EmbeddingProjectionService>();

                var allowedOrigins = builder.Configuration
                    .GetSection("AllowedOrigins").Get<string[]>();

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
                            IssuerSigningKey = new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(jwtSecret)),
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
                    options.UseNpgsql(
                        builder.Configuration.GetConnectionString("DefaultConnection"),
                        o => o.UseVector())); // ← pgvector extension

                builder.Services.AddScoped(sp =>
                    sp.GetRequiredService<IDbContextFactory<SqlDbContext>>().CreateDbContext());

                builder.Services.AddScoped<ChatService>();
                builder.Services.AddScoped<ContactService>();
                builder.Services.AddScoped<AdminService>();
                builder.Services.AddSingleton<MailgunService>();

                // Behind Nginx / Railway reverse proxy
                builder.Services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders =
                        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    options.KnownProxies.Clear();
                    options.KnownNetworks.Clear();
                });

                var app = builder.Build();

                app.UseForwardedHeaders();
                app.UseSerilogRequestLogging();

                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }

                app.UseMiddleware<ExceptionLoggerMiddleware>();
                app.UseExceptionHandler("/error");
                app.UseRateLimiter();
                app.UseCors(PublicCorsPolicy);
                app.UseAuthentication();
                app.UseAuthorization();
                app.MapControllers();

                // Auto-migrate on startup
                using (var scope = app.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<SqlDbContext>();
                    var pending = db.Database.GetPendingMigrations().ToList();
                    if (pending.Any())
                    {
                        Log.Information("Applying {Count} pending migrations: {Migrations}",
                            pending.Count, string.Join(", ", pending));
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
