using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using TransactionalEmail.Contracts;

namespace TransactionalEmail.Mandrill;

public sealed class MandrillEmailSender(HttpClient httpClient, string apiKey, string fromEmail)
{
    public async Task<EmailSendResult> SendTemplateAsync(EmailRequest request, string templateSlug, CancellationToken cancellationToken = default)
    {
        var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString("N");
        var payload = new
        {
            key = apiKey,
            template_name = templateSlug,
            template_content = Array.Empty<object>(),
            message = new
            {
                from_email = fromEmail,
                subject = $"POC {templateSlug}",
                to = request.To.Select(recipient => new { email = recipient.Email, name = recipient.Name, type = "to" }),
                merge_language = "handlebars",
                global_merge_vars = BuildMergeVars(request.Data)
            },
            @async = false
        };

        using var response = await httpClient.PostAsJsonAsync("https://mandrillapp.com/api/1.0/messages/send-template.json", payload, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        var body = JsonSerializer.Deserialize<JsonElement>(raw);
        
        MandrillResponse[]? results = null;
        if (body.ValueKind == JsonValueKind.Array)
        {
            results = body.Deserialize<MandrillResponse[]>();
        }
        else if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "error")
        {
            var error = body.Deserialize<MandrillError>();
            return new EmailSendResult(false, "error", correlationId, null, request.SourceSystem, request.TemplateKey, error?.Message ?? "Mandrill error");
        }

        var result = results?.FirstOrDefault();
        var accepted = response.IsSuccessStatusCode && result?.Status is "sent" or "queued";
        return new EmailSendResult(accepted, result?.Status ?? $"http-{(int)response.StatusCode}", correlationId,
            result?.MessageId, request.SourceSystem, request.TemplateKey, accepted ? null : result?.RejectReason ?? response.ReasonPhrase);
    }

    private static IEnumerable<object> BuildMergeVars(IReadOnlyDictionary<string, object?> data)
    {
        var list = new List<object>();
        foreach (var item in data)
        {
            var jsonNode = JsonNode.Parse(JsonSerializer.Serialize(item.Value));
            list.Add(new { name = item.Key, content = jsonNode });
        }
        return list;
    }

    private sealed class MandrillResponse
    {
        public string? Status { get; set; }
        public string? _id { get; set; }
        public string? reject_reason { get; set; }
        public string? MessageId => _id;
        public string? RejectReason => reject_reason;
    }

    private sealed class MandrillError
    {
        public string? Status { get; set; }
        public int Code { get; set; }
        public string? Name { get; set; }
        public string? Message { get; set; }
    }
}
