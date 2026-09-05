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

    /// <summary>
    /// Merge data for the "Assessment Booked" template (id 10128760).
    /// Includes appointment-specific placeholders used in physiocouncil transactional emails.
    /// </summary>
    public static IReadOnlyDictionary<string, string> AssessmentBookedMergeData { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FNAME"] = "Preetham",
            ["LNAME"] = "Kh",
            ["EMAIL"] = "preetham.kh@physiocouncil.com.au",
            ["COMPANY"] = "Australian Physiotherapy Council",
            ["ASSESSMENT_DATE"] = "Monday, 25 August 2026",
            ["ASSESSMENT_TIME"] = "10:30 AM",
            ["PRACTITIONER"] = "Dr Sarah Chen",
            ["LOCATION"] = "Melbourne Clinic, Level 2",
            ["BOOKING_ID"] = "APC-2026-08-0012"
        };

    /// <summary>
    /// Sample HTML for the "Assessment Booked" template, with *|MAILCHIMP|* merge tags that
    /// Mandrill renders server-side. Used when the Mailchimp API doesn't return raw HTML
    /// for multichannel/drag-and-drop templates.
    /// </summary>
    public const string AssessmentBookedSampleHtml = """
    <html>
      <body style="font-family: Arial, sans-serif; color:#222;">
        <div style="background:#00467f; color:#fff; padding:16px;">
          <h1>*|COMPANY|*</h1>
        </div>
        <p>Hi *|FNAME|* *|LNAME|*,</p>
        <p>Your physiotherapy assessment has been booked.</p>
        <table style="border-collapse:collapse; margin:16px 0;">
          <tr><td><strong>Date:</strong></td><td>&nbsp;*|ASSESSMENT_DATE|*</td></tr>
          <tr><td><strong>Time:</strong></td><td>&nbsp;*|ASSESSMENT_TIME|*</td></tr>
          <tr><td><strong>Practitioner:</strong></td><td>&nbsp;*|PRACTITIONER|*</td></tr>
          <tr><td><strong>Location:</strong></td><td>&nbsp;*|LOCATION|*</td></tr>
          <tr><td><strong>Booking ID:</strong></td><td>&nbsp;*|BOOKING_ID|*</td></tr>
        </table>
        <p>We'll send a reminder 24 hours before your appointment.</p>
        <footer style="color:#888; font-size:12px;">POC only &mdash; not production content.</footer>
      </body>
    </html>
    """;

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
