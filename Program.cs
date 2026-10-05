using BiometricService;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Logging;

using var singleInstance = new Mutex(true, @"Local\Fingertech-API-SingleInstance", out bool createdNew);
if (!createdNew)
{
    return;
}

static void LogCrash(string source, Exception ex)
{
    var text = $"[{DateTime.Now:u}] {source}\n{ex}\n";
    try
    {
        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fingertech-API", "logs");
        Directory.CreateDirectory(logDir);
        File.AppendAllText(Path.Combine(logDir, "crash.log"), text);
    }
    catch { }
    try { System.Diagnostics.EventLog.WriteEntry("Biometric API Service", text, System.Diagnostics.EventLogEntryType.Error); } catch { }
}

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    if (e.ExceptionObject is Exception ex) LogCrash("UnhandledException", ex);
};
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    LogCrash("UnobservedTaskException", e.Exception);
    e.SetObserved();
};

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

serviceApp.UseExceptionHandler(app => app.Run(async ctx =>
{
    var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    ctx.Response.StatusCode = 500;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsync($"{{\"message\":\"Error: {ex?.Message}\",\"success\":false}}");
}));

serviceApp.UseRouting();
serviceApp.UseCors("DefaultCorsPolicy");
serviceApp.MapControllers();

var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
serviceApp.MapGet("/apiservice", () => Results.Json(new { success = true, message = "Biometric API Service running", version }));
serviceApp.MapGet("/apiservice/status", () => Results.Json(new { success = true, message = "Biometric API Service running", version }));

try
{
    serviceApp.Run();
}
catch (Exception ex)
{
    LogCrash("Startup", ex);
    Environment.Exit(1);
}