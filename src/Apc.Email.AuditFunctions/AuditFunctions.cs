using System.Net;
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
    public Task EmailAuditConsumer(
        [ServiceBusTrigger("email-events", Connection = "ServiceBusConnection")] string message,
        FunctionContext context)
    {
        // Production implementation writes the event to SQL, Blob Storage, or D365 asynchronously.
        return Task.CompletedTask;
    }
}
