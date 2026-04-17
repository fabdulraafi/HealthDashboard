using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using SendGrid;
using SendGrid.Helpers.Mail;

public class DatabaseHealthPublisher : IHealthCheckPublisher
{
    private readonly string _connectionString;
    private readonly string _sendGridApiKey;
    private readonly Dictionary<string, HealthStatus> _previousStatuses = new();


    public DatabaseHealthPublisher(IConfiguration configuration)
    {
        
        _connectionString = configuration.GetConnectionString("MicroserviceDb");
    }

    public async Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);

            foreach (var entry in report.Entries)
            {
                // 
                var sql = "INSERT INTO HealthLogs (ServiceId, Status, ResponseTimeMs, ErrorMessage, Timestamp) " +
                          "VALUES (@name, @status, @time, @error, @date)";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@name", entry.Key);
                    command.Parameters.AddWithValue("@status", entry.Value.Status.ToString());
                    command.Parameters.AddWithValue("@time", (int)entry.Value.Duration.TotalMilliseconds);
                    command.Parameters.AddWithValue("@error", entry.Value.Description ?? "No error");
                    command.Parameters.AddWithValue("@date", DateTime.Now);

                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }

        foreach (var entry in report.Entries)
        {
            var serviceName = entry.Key;
            var currentStatus = entry.Value.Status;

            // Check if we have a previous status to compare against
            if (_previousStatuses.TryGetValue(serviceName, out var previousStatus))
            {
                // Status changed to Unhealthy
                if (previousStatus != HealthStatus.Unhealthy && currentStatus == HealthStatus.Unhealthy)
                {
                    await SendAlertEmailAsync(
                        subject: $"🔴 ALERT: {serviceName} is Unhealthy",
                        body: $"Service <strong>{serviceName}</strong> has gone <strong>Unhealthy</strong>.<br/><br/>" +
                              $"Error: {entry.Value.Description ?? "No details available"}<br/>" +
                              $"Time: {DateTime.Now}"
                    );
                }
                // Status recovered to Healthy
                else if (previousStatus == HealthStatus.Unhealthy && currentStatus == HealthStatus.Healthy)
                {
                    await SendAlertEmailAsync(
                        subject: $"✅ RECOVERED: {serviceName} is back to Healthy",
                        body: $"Service <strong>{serviceName}</strong> has recovered and is back to <strong>Healthy</strong>.<br/><br/>" +
                              $"Time: {DateTime.Now}"
                    );
                }
            }

            // Update the stored status for next comparison
            _previousStatuses[serviceName] = currentStatus;
        }
    }

    private async Task SendAlertEmailAsync(string subject, string body)
    {
        var client = new SendGridClient(_sendGridApiKey);
        var msg = new SendGridMessage
        {
            From = new EmailAddress("abdulraafi98@gmail.com", "Health Dashboard"),
            Subject = subject,
            HtmlContent = body
        };
        msg.AddTo(new EmailAddress("abdulraafi98@gmail.com"));

        await client.SendEmailAsync(msg);
    }
}
    
