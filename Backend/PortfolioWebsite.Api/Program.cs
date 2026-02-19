
using Amazon.BedrockRuntime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Middlewares;
using PortfolioWebsite.Api.Services;
using Serilog;

namespace PortfolioWebsite.Api
{
    public class Program
    {

        const string CorsPolicy = "ProductionCors";

        public static void Main(string[] args)
        {
            try
            {

                var builder = WebApplication.CreateBuilder(args);

                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .CreateLogger();

                builder.Services.AddControllers();
                // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen();
                builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
                builder.Services.AddSingleton<IAmazonBedrockRuntime>(_ =>
                {
                    var region = Amazon.RegionEndpoint.GetBySystemName(
                        Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1");

                    return new AmazonBedrockRuntimeClient(region);
                });

                var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
                builder.Services.AddCors(options =>
                {
                    options.AddPolicy(CorsPolicy,
                        policy => policy.WithOrigins(allowedOrigins ?? Array.Empty<string>())
                                        .AllowAnyMethod()
                                        .AllowAnyHeader());
                });


                builder.Services.AddDbContext<SqlDbContext>(options =>
                {
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
                });
                builder.Services.AddScoped<ChatService>();
                builder.Services.AddScoped<ContactService>();
                builder.Services.AddSingleton<MailgunService>();

                builder.Logging.AddSerilog();

                Log.Information("Logging!");

                var app = builder.Build();
                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }

                app.UseMiddleware<ExceptionLoggerMiddleware>();
                app.UseExceptionHandler("/error");
                app.UseHttpsRedirection();

                app.UseCors(CorsPolicy);


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
