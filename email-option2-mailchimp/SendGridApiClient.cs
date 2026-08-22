using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MailchimpPoc;

public sealed record SendResult(bool IsSuccess, HttpStatusCode StatusCode, string ErrorBody);

public sealed class SendGridApiClient
{
    private const string SendEndpoint = "https://api.sendgrid.com/v3/mail/send";
    private readonly HttpClient _client = new();

    public SendGridApiClient(string apiKey)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<SendResult> SendHtmlAsync(string subject, string html, string toEmail)
    {
        var payload = new
        {
            personalizations = new[] { new { to = new[] { new { email = toEmail } } } },
            subject,
            from = new { email = toEmail, name = "Transactional Email POC" },
            content = new[]
            {
                new { type = "text/plain", value = ToPlainText(html) },
                new { type = "text/html", value = html }
            }
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync(SendEndpoint, content);
        var body = await response.Content.ReadAsStringAsync();

        // SendGrid returns 202 Accepted with an empty body on success.
        return response.IsSuccessStatusCode
            ? new SendResult(true, response.StatusCode, string.Empty)
            : new SendResult(false, response.StatusCode, body);
    }

    private static string ToPlainText(string html)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        return System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim());
    }
}
