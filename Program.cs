using System.Data.Common;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// --- 1. THE DATA STATE ---
// We create a simple object to hold our "Health" status
var mySystemState = new SystemState { IsHealthy = true };
builder.Services.AddSingleton(mySystemState);

// --- 2. THE SERVICES ---
builder.Services.AddHealthChecksUI(setup =>
{
    setup.AddHealthCheckEndpoint("Main Hub", "/health");
})
.AddInMemoryStorage();

builder.Services.AddHealthChecks()
    .AddCheck("Manual_Kill_Switch", () =>
        mySystemState.IsHealthy
        ? HealthCheckResult.Healthy("Systems Nominal")
        : HealthCheckResult.Unhealthy("Manual Failure Active"))
    .AddSqlServer(connectionString: builder.Configuration.GetConnectionString("MicroserviceDb"), name: "Centralized_SQL_DB");

var app = builder.Build();

// --- 3. THE ROUTES ---
app.UseRouting();

// Testing Routes
app.MapGet("/break", (SystemState state) => { state.IsHealthy = false; return "System Broken"; });
app.MapGet("/fix", (SystemState state) => { state.IsHealthy = true; return "System Fixed"; });

app.UseEndpoints(endpoints =>
{
    // Dashboard: http://localhost:5154/dashboard
    endpoints.MapHealthChecksUI(setup => { setup.UIPath = "/dashboard"; });
    
    // API Data: http://localhost:5154/health
    endpoints.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
});

app.Run();

// THE CLASS DEFINITION
public class SystemState { public bool IsHealthy { get; set; } }