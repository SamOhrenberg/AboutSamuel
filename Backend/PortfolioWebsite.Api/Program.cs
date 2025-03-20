
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
                builder.Services.AddHttpClient();

                builder.Services.AddCors(options =>
                {
                    options.AddPolicy("AllowLocalhost",
                        builder => builder
                            .AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .WithExposedHeaders("X-Token-Limit-Reached")
                    );
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

                app.UseHttpsRedirection();

                app.UseCors("AllowLocalhost");


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
