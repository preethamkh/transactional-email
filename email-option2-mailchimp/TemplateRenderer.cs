using System.Text.Json;

namespace MailchimpPoc;

public static class TemplateRenderer
{
    /// <summary>
    /// Replaces Mailchimp-style merge tags (*|TAG|*) with supplied values.
    /// Client-side preview only: authoritative rendering happens in Mandrill at send time.
    /// </summary>
    public static string Render(string templateHtml, IReadOnlyDictionary<string, string> mergeData)
    {
        if (string.IsNullOrEmpty(templateHtml)) return string.Empty;

        var rendered = templateHtml;
        foreach ((var tag, var value) in mergeData)
        {
            rendered = rendered.Replace(MergeTag(tag), value, StringComparison.OrdinalIgnoreCase);
        }
        return rendered;
    }

    private static string MergeTag(string name) => $"*|{name.ToUpperInvariant()}|*";

    public static IReadOnlyDictionary<string, string> SampleMergeData { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FNAME"] = "Jane",
            ["LNAME"] = "Citizen",
            ["EMAIL"] = "jane.citizen@example.org",
            ["COMPANY"] = "Australian Physiotherapy Council"
        };

    public const string SampleTemplateHtml = """
        <html>
          <body style="font-family: Arial, sans-serif; color:#222;">
            <div style="background:#00467f; color:#fff; padding:16px;">
              <h1>*|COMPANY|*</h1>
            </div>
            <p>Hi *|FNAME|* *|LNAME|*,</p>
            <p>This is the POC dummy template. If you can read this, retrieval and rendering worked.</p>
            <p>We sent this to *|EMAIL|*.</p>
            <footer style="color:#888; font-size:12px;">POC only &mdash; not production content.</footer>
          </body>
        </html>
        """;
}
