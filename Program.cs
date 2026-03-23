using BiometricService;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Read CORS configuration from appsettings
var corsSection = builder.Configuration.GetSection("Cors");
bool allowAnyOrigin = corsSection.GetValue<bool>("AllowAnyOrigin", false);
var allowedOrigins = corsSection.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddWindowsService();
LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);
builder.Services.AddScoped<Biometric>();
builder.Services.AddSingleton<APIService>();
builder.Services.AddHostedService<APIService>();
builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        if (allowAnyOrigin)
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            // No origins configured; block cross-origin by default
            policy.SetIsOriginAllowed(origin => false);
        }
    });
});

var serviceApp = builder.Build();

// Log configured CORS values
var logger = serviceApp.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("CORS configuration - AllowAnyOrigin: {AllowAnyOrigin}, AllowedOrigins: {AllowedOrigins}", allowAnyOrigin, string.Join(',', allowedOrigins));

serviceApp.UseRouting();
serviceApp.UseCors("DefaultCorsPolicy");
serviceApp.UseCors();
serviceApp.MapControllers();

serviceApp.Run();