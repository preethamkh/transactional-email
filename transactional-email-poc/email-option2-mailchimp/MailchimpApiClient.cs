using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace MailchimpPoc;

public sealed record ApiResult(bool IsSuccess, HttpStatusCode StatusCode, string Body);

/// <summary>
/// Minimal Mailchimp Marketing API client (Basic auth).
/// The API key format is "{key}-{datacenter}"; the datacenter drives the base URL.
/// </summary>
public sealed class MailchimpApiClient
{
    private readonly HttpClient _client = new();

    public MailchimpApiClient(string apiKey)
    {
        var (token, datacenter) = SplitApiKey(apiKey);
        var baseUrl = $"https://{datacenter}.api.mailchimp.com/3.0/";
        _client.BaseAddress = new Uri(baseUrl);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"anyuser:{token}")));
    }

    public async Task<ApiResult> GetAsyncAsync(string relativeUrl)
    {
        var normalized = NormalizeUrl(relativeUrl);
        using var response = await _client.GetAsync(normalized);
        var body = await response.Content.ReadAsStringAsync();
        return new ApiResult(response.IsSuccessStatusCode, response.StatusCode, body);
    }

    private static (string Token, string Datacenter) SplitApiKey(string apiKey)
    {
        var separatorIndex = apiKey.LastIndexOf('-');
        if (separatorIndex <= 0 || separatorIndex == apiKey.Length - 1)
        {
            throw new FormatException(
                "Mailchimp API key must be in the format '{key}-{datacenter}' (e.g. 'abc123-us21'). " +
                "Copy the full value from Mailchimp > Account > Extras > API keys.");
        }

        return (apiKey[..separatorIndex], apiKey[(separatorIndex + 1)..]);
    }

    private static string NormalizeUrl(string relativeUrl)
    {
        // HttpClient combines BaseAddress (with trailing /) + relativeUrl.
        // Strip any leading slash to keep the path under /3.0/.
        return relativeUrl.TrimStart('/');
    }
}
