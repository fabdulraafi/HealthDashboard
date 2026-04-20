using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using SendGrid;
using SendGrid.Helpers.Mail;

public class DatabaseHealthPublisher : IHealthCheckPublisher
{
    private readonly string _connectionString;
    private readonly string _sendGridApiKey;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, HealthStatus> _previousStatuses = new();

    public DatabaseHealthPublisher(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("MicroserviceDb") ?? "";
        _sendGridApiKey = configuration["SendGrid:ApiKey"] ?? "";
        Console.WriteLine($"[PUBLISHER] SendGrid key loaded: {(_sendGridApiKey.Length > 0 ? _sendGridApiKey.Substring(0, 8) + "..." : "EMPTY")}");
    }


    public async Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[PUBLISHER] Running at {DateTime.Now}");

        // --- 1. LOG TO DATABASE (wrapped so failure doesn't stop alerting) ---
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            foreach (var entry in report.Entries)
            {
                var sql = "INSERT INTO HealthLogs (ServiceId, Status, ResponseTimeMs, ErrorMessage, Timestamp) " +
                          "VALUES (@name, @status, @time, @error, @date)";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@name", entry.Key);
                command.Parameters.AddWithValue("@status", entry.Value.Status.ToString());
                command.Parameters.AddWithValue("@time", (int)entry.Value.Duration.TotalMilliseconds);
                command.Parameters.AddWithValue("@error", entry.Value.Description ?? "No error");
                command.Parameters.AddWithValue("@date", DateTime.Now);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PUBLISHER] DB logging failed: {ex.Message}");
        }

        // --- 2. CHECK FOR STATUS CHANGES AND ALERT ---
        foreach (var entry in report.Entries)
        {
            var serviceName = entry.Key;
            var currentStatus = entry.Value.Status;

            Console.WriteLine($"[PUBLISHER] {serviceName} = {currentStatus}");

            if (_previousStatuses.TryGetValue(serviceName, out var previousStatus))
            {
                if (previousStatus != HealthStatus.Unhealthy && currentStatus == HealthStatus.Unhealthy)
                {
                    Console.WriteLine($"[PUBLISHER] ALERT: {serviceName} went Unhealthy");
                    await SendAlertEmailAsync(
                        subject: $"ALERT: {serviceName} is Unhealthy",
                        body: $"Service <strong>{serviceName}</strong> has gone <strong>Unhealthy</strong>.<br/><br/>" +
                              $"Error: {entry.Value.Description ?? "No details available"}<br/>" +
                              $"Time: {DateTime.Now}"
                    );
                }
                else if (previousStatus == HealthStatus.Unhealthy && currentStatus == HealthStatus.Healthy)
                {
                    Console.WriteLine($"[PUBLISHER] RECOVERED: {serviceName} is back to Healthy");
                    await SendAlertEmailAsync(
                        subject: $"RECOVERED: {serviceName} is back to Healthy",
                        body: $"Service <strong>{serviceName}</strong> has recovered and is back to <strong>Healthy</strong>.<br/><br/>" +
                              $"Time: {DateTime.Now}"
                    );
                }
            }
            else
            {
                Console.WriteLine($"[PUBLISHER] First time seeing {serviceName}, storing status");
            }

            _previousStatuses[serviceName] = currentStatus;
        }
    }

    private async Task SendAlertEmailAsync(string subject, string body)
    {
        try
        {
            Console.WriteLine($"[PUBLISHER] Sending email: {subject}");
            
            using var client = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587);
            client.EnableSsl = true;
            client.Credentials = new System.Net.NetworkCredential(
                _configuration["Gmail:Email"],
                _configuration["Gmail:AppPassword"]
            );

            var message = new System.Net.Mail.MailMessage(
                from: _configuration["Gmail:Email"]!,
                to: _configuration["Gmail:Email"]!,
                subject: subject,
                body: body
            );
            message.IsBodyHtml = true;

            await client.SendMailAsync(message);
            Console.WriteLine($"[PUBLISHER] Email sent successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PUBLISHER] Email failed: {ex.Message}");
        }
    }
}