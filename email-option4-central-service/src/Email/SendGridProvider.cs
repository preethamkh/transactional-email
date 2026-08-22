using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EmailCentral.Api.Domain;
using EmailCentral.Api.Logging;

namespace EmailCentral.Api.Email;

/// <summary>
/// SendGrid REST v3 provider using Dynamic Templates.
/// Deliberately does NOT use ShareIt.Library.SendGrid: the central service owns the provider dependency,
/// which is the seam that keeps the vendor swappable.
/// </summary>
public sealed class SendGridProvider : IEmailProvider
{
    private const string SendEndpoint = "https://api.sendgrid.com/v3/mail/send";

    private readonly HttpClient _client = new();
    private readonly ActivityLog _activityLog;

    public SendGridProvider(string? apiKey, ActivityLog activityLog)
    {
        _activityLog = activityLog;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public string Name => "sendgrid";

    public async Task<ProviderSendResult> SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default)
    {
        var payload = SendGridPayloadBuilder.BuildPayload(email);
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync(SendEndpoint, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // SendGrid returns 202 Accepted with an empty body; the X-Message-Id header carries the id.
        var messageId = response.Headers.TryGetValues("X-Message-Id", out var values)
            ? values.FirstOrDefault()
            : null;

        return response.IsSuccessStatusCode
            ? new ProviderSendResult(true, messageId ?? "unknown", null)
            : new ProviderSendResult(false, null, $"HTTP {(int)response.StatusCode}: {body}");
    }
}

/// <summary>Builds the SendGrid v3 mail/send payload. Public and side-effect free for unit testing.</summary>
public static class SendGridPayloadBuilder
{
    public static object BuildPayload(OutgoingEmail email)
    {
        var personalizations = new object[]
        {
            new
            {
                to = email.To.Select(r => new { email = r.Email, name = r.Name }).ToArray(),
                custom_args = email.CustomArgs
            }
        };

        return new
        {
            from = new { email = email.From.FromEmail, name = email.From.FromName },
            subject = email.Subject,
            personalizations,
            template_id = email.ProviderTemplateId,
            dynamic_template_data = email.TemplateData ?? new Dictionary<string, object?>()
        };
    }
}
