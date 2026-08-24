namespace EmailCentral.Api.Templates;

/// <summary>
/// Naive {{token}} substitution for preview purposes only.
/// Authoritative rendering is performed by the provider (SendGrid handlebars) at send time.
/// </summary>
public static class PreviewRenderer
{
    public static string Render(string html, IReadOnlyDictionary<string, object?>? data)
    {
        if (data is null || data.Count == 0) return html;

        var rendered = html;
        foreach ((var key, var value) in data)
        {
            rendered = rendered.Replace($"{{{{{key}}}}}", value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        return rendered;
    }
}
