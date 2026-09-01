using System.Net.Http.Json;
using Apc.Email.Contracts;

namespace Apc.Email.Mandrill;

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
                merge_language = "mailchimp",
                global_merge_vars = request.Data.Select(item => new { name = item.Key, content = item.Value?.ToString() ?? string.Empty })
            },
            @async = false
        };

        using var response = await httpClient.PostAsJsonAsync("https://mandrillapp.com/api/1.0/messages/send-template.json", payload, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<MandrillResponse[]>(cancellationToken: cancellationToken);
        var result = body?.FirstOrDefault();
        var accepted = response.IsSuccessStatusCode && result?.Status is "sent" or "queued";
        return new EmailSendResult(accepted, result?.Status ?? $"http-{(int)response.StatusCode}", correlationId,
            result?.MessageId, request.SourceSystem, request.TemplateKey, accepted ? null : result?.RejectReason ?? response.ReasonPhrase);
    }

    private sealed class MandrillResponse
    {
        public string? Status { get; set; }
        public string? _id { get; set; }
        public string? reject_reason { get; set; }
        public string? MessageId => _id;
        public string? RejectReason => reject_reason;
    }
}
