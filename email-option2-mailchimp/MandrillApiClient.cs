using System.Net;
using System.Text;
using System.Text.Json;

namespace MailchimpPoc;

public sealed record MandrillSendResult(bool IsSuccess, string Status, string? MessageId, string? RejectReason);

/// <summary>
/// Minimal Mandrill (Mailchimp Transactional) API client. All endpoints are POST with the API key in the body.
/// Demo/free tier: sends only to recipients at an authenticated domain; ~25 emails/hour.
/// </summary>
public sealed class MandrillApiClient
{
    private const string BaseUrl = "https://mandrillapp.com/api/1.0";
    private readonly string _apiKey;
    private readonly HttpClient _client = new();

    public MandrillApiClient(string apiKey) => _apiKey = apiKey;

    /// <summary>Connectivity + key check. Returns "PONG!" on success.</summary>
    public async Task<ApiResult> PingAsync() =>
        await PostAsync("/users/ping.json", new { key = _apiKey });

    public async Task<MandrillSendResult> SendHtmlAsync(
        string fromEmail,
        string fromName,
        string subject,
        string html,
        string toEmail,
        IReadOnlyDictionary<string, string> globalMergeVars)
    {
        // merge_language=mailchimp lets Mandrill render *|TAG|* server-side,
        // which is exactly how Option 2 would work for non-rendered template HTML.
        var payload = new
        {
            key = _apiKey,
            message = new
            {
                from_email = fromEmail,
                from_name = fromName,
                subject,
                html,
                to = new[] { new { email = toEmail, type = "to" } },
                merge_language = "mailchimp",
                global_merge_vars = globalMergeVars.Select(kv => new { name = kv.Key, content = kv.Value }).ToArray(),
                auto_text = true
            },
            @async = false
        };

        var (ok, status, body) = await PostAsync("/messages/send.json", payload);
        if (!ok)
        {
            return new MandrillSendResult(false, $"http-{(int)status}", null, body);
        }

        using var doc = JsonDocument.Parse(body);
        var first = doc.RootElement.EnumerateArray().FirstOrDefault();
        if (first.ValueKind == JsonValueKind.Undefined)
        {
            return new MandrillSendResult(false, "empty-response", null, body);
        }

        var sendStatus = first.TryGetProperty("status", out var s) ? s.GetString() ?? "unknown" : "unknown";
        var id = first.TryGetProperty("_id", out var i) ? i.GetString() : null;
        var rejectReason = first.TryGetProperty("reject_reason", out var r) ? r.GetString() : null;

        return new MandrillSendResult(sendStatus is "sent" or "queued", sendStatus, id, rejectReason);
    }

    private async Task<ApiResult> PostAsync(string relativeUrl, object payload)
    {
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync($"{BaseUrl}{relativeUrl}", content);
        var body = await response.Content.ReadAsStringAsync();
        return new ApiResult(response.IsSuccessStatusCode, response.StatusCode, body);
    }
}
