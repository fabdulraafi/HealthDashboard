using Serilog;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// --- 1. THE DATA STATE ---
var mySystemState = new SystemState { IsHealthy = true };
builder.Services.AddSingleton(mySystemState);

// --- 2. THE SERVICES ---
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient();

builder.Services.AddHealthChecks()
    .AddCheck("Manual_Kill_Switch", () =>
        mySystemState.IsHealthy
        ? HealthCheckResult.Healthy("Systems Nominal")
        : HealthCheckResult.Unhealthy("Manual Failure Active"))
    .AddSqlServer(connectionString: builder.Configuration.GetConnectionString("MicroserviceDb"), name: "Centralized_SQL_DB")
    .AddUrlGroup(new Uri("http://localhost:8081/health"), name: "Seq_Server");

builder.Services.AddSingleton<IHealthCheckPublisher, DatabaseHealthPublisher>();

builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Period = TimeSpan.FromMinutes(1);
    options.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// --- 3. THE ROUTES ---
app.UseStaticFiles();
app.UseRouting();

app.MapGet("/break", (SystemState state) => { state.IsHealthy = false; return "System Broken"; });
app.MapGet("/fix", (SystemState state) => { state.IsHealthy = true; return "System Fixed"; });

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

public class SystemState { public bool IsHealthy { get; set; } }