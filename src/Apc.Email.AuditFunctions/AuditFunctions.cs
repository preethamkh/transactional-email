using System.Net;
using System.Data;
using System.Text.Json;
using Apc.Email.Contracts;
using Microsoft.Data.SqlClient;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Apc.Email.AuditFunctions;

public sealed class AuditFunctions
{
    [Function("MandrillWebhook")]
    public async Task<HttpResponseData> MandrillWebhook(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "events/mandrill")] HttpRequestData request)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { received = true, note = "Production version should validate the Mandrill webhook and enqueue the event." });
        return response;
    }

    [Function("EmailAuditConsumer")]
    public async Task EmailAuditConsumer(
        [ServiceBusTrigger("email-events", Connection = "ServiceBusConnection")] string message,
        FunctionContext context)
    {
        var audit = JsonSerializer.Deserialize<EmailAuditRecord>(message)
            ?? throw new InvalidDataException("The email audit message was empty or invalid.");
        if (string.IsNullOrWhiteSpace(audit.CorrelationId) || string.IsNullOrWhiteSpace(audit.SourceSystem) ||
            string.IsNullOrWhiteSpace(audit.TemplateKey) || string.IsNullOrWhiteSpace(audit.Recipient) ||
            string.IsNullOrWhiteSpace(audit.Status))
            throw new InvalidDataException("The email audit message is missing required fields.");

        var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString")
            ?? throw new InvalidOperationException("SqlConnectionString is not configured.");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.EmailAudit
                (OccurredAt, CorrelationId, SourceSystem, TemplateKey, Recipient, Status, ProviderMessageId, Error, DataJson)
            VALUES
                (@OccurredAt, @CorrelationId, @SourceSystem, @TemplateKey, @Recipient, @Status, @ProviderMessageId, @Error, @DataJson);
            """;
        command.Parameters.Add("@OccurredAt", SqlDbType.DateTimeOffset).Value = audit.OccurredAt;
        command.Parameters.Add("@CorrelationId", SqlDbType.NVarChar, 100).Value = audit.CorrelationId;
        command.Parameters.Add("@SourceSystem", SqlDbType.NVarChar, 100).Value = audit.SourceSystem;
        command.Parameters.Add("@TemplateKey", SqlDbType.NVarChar, 200).Value = audit.TemplateKey;
        command.Parameters.Add("@Recipient", SqlDbType.NVarChar, 320).Value = audit.Recipient;
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = audit.Status;
        command.Parameters.Add("@ProviderMessageId", SqlDbType.NVarChar, 200).Value = (object?)audit.ProviderMessageId ?? DBNull.Value;
        command.Parameters.Add("@Error", SqlDbType.NVarChar, 2000).Value = (object?)audit.Error ?? DBNull.Value;
        command.Parameters.Add("@DataJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(audit.Data);
        await command.ExecuteNonQueryAsync();
    }
}
