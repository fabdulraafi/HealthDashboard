using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;

public class DatabaseHealthPublisher : IHealthCheckPublisher
{
    private readonly string _connectionString;

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
    }
}