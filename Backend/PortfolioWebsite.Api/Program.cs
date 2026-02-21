using Amazon.BedrockRuntime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Middlewares;
using PortfolioWebsite.Api.Services;
using Serilog;
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
            try
            {
                var builder = WebApplication.CreateBuilder(args);

                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .CreateLogger();

                builder.Services.AddControllers();
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen();

                builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
                builder.Services.AddSingleton<IAmazonBedrockRuntime>(_ =>
                {
                    var region = Amazon.RegionEndpoint.GetBySystemName(
                        Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1");
                    return new AmazonBedrockRuntimeClient(region);
                });

                var allowedOrigins = builder.Configuration
                    .GetSection("AllowedOrigins").Get<string[]>();

                builder.Services.AddCors(options =>
                {
                    options.AddPolicy(PublicCorsPolicy,
                        policy => policy
                            .WithOrigins(allowedOrigins ?? Array.Empty<string>())
                            .AllowAnyMethod()
                            .AllowAnyHeader());

                    options.AddPolicy(AdminCorsPolicy,
                        policy => policy
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
                            ClockSkew = TimeSpan.Zero // No grace period — token expiry is exact
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

                builder.Services.AddDbContext<SqlDbContext>(options =>
                {
                    options.UseSqlServer(
                        builder.Configuration.GetConnectionString("DefaultConnection"));
                });

                builder.Services.AddDbContextFactory<SqlDbContext>(options =>
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

                builder.Services.AddScoped<ChatService>();
                builder.Services.AddScoped<ContactService>();
                builder.Services.AddScoped<AdminService>();
                builder.Services.AddSingleton<MailgunService>();

                builder.Logging.AddSerilog();

                Log.Information("Starting PortfolioWebsite.Api");

                var app = builder.Build();

                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }

                app.UseMiddleware<ExceptionLoggerMiddleware>();
                app.UseExceptionHandler("/error");
                app.UseHttpsRedirection();
                app.UseRateLimiter();

                app.MapControllers().RequireCors(PublicCorsPolicy);

                app.UseCors(PublicCorsPolicy);
                app.UseAuthentication();
                app.UseAuthorization();

                app.MapControllers();

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, ex.Message);
            }
        }
    }
}