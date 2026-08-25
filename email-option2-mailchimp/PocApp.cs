using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace MailchimpPoc;

public static class PocApp
{
    private const string SelfTestArg = "selftest";

    public static async Task<int> RunAsync(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(PocApp).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var settings = PocSettings.FromConfiguration(configuration);
        var log = new JsonLinesLogger(settings.LogsDirectory);

        if (args.Any(a => a.Equals(SelfTestArg, StringComparison.OrdinalIgnoreCase)))
        {
            return await SelfTestAsync(settings, log);
        }

        var templateName = ExtractArgValue(args, "--mandrill-template");
        if (templateName is not null)
        {
            await MandrillTemplatePipelineAsync(settings, log, templateName);
            return 0;
        }

        var templateId = ExtractTemplateId(args) ?? AssessmentBookedTemplateId;

        if (args.Any(a => a.Equals("--sendgrid", StringComparison.OrdinalIgnoreCase)))
        {
            await FullPipelineAsync(settings, log, templateId);
            return 0;
        }

        if (args.Any(a => a.Equals("--mandrill", StringComparison.OrdinalIgnoreCase)))
        {
            await FullPipelineViaMandrillAsync(settings, log, templateId);
            return 0;
        }

        return await MenuLoopAsync(settings, log);
    }

    private static async Task<int> MenuLoopAsync(PocSettings settings, JsonLinesLogger log)
    {
        while (true)
        {
            PrintMenu(settings);
            var choice = Console.ReadLine()?.Trim();
            try
            {
                switch (choice)
                {
                    case "1": await ListTemplatesAsync(settings, log); break;
                    case "2": await GetTemplateAsync(settings, log); break;
                    case "3": RenderSample(); break;
                    case "4": await FullPipelineAsync(settings, log); break;
                    case "5": await FullPipelineViaMandrillAsync(settings, log); break;
                    case "6": await MandrillTemplateByNameAsync(settings, log); break;
                    case "7": return 0;
                    default: Console.WriteLine("Unknown option."); break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                await log.WriteAsync("menu", choice ?? "?", "error", 0, ex.Message);
            }
        }
    }

    private static void PrintMenu(PocSettings settings)
    {
        Console.WriteLine();
        Console.WriteLine("=== Mailchimp Template POC (Option 2 validation) ===");
        Console.WriteLine($"Mailchimp key : {Describe(settings.MailchimpApiKey)}");
        Console.WriteLine($"Mandrill key  : {Describe(settings.MandrillApiKey)}");
        Console.WriteLine($"SendGrid key  : {Describe(settings.SendGridApiKey)}");
        Console.WriteLine($"Recipient     : {settings.ToEmail ?? "(not set)"}");
        Console.WriteLine($"From email    : {settings.FromEmail ?? "(defaults to no-reply@...)"}");
        Console.WriteLine();
        Console.WriteLine(" 1. List templates            (Mailchimp API)");
        Console.WriteLine(" 2. Get template HTML by ID   (saved to logs/)");
        Console.WriteLine(" 3. Render sample merge tags  (offline proof)");
        Console.WriteLine(" 4. FULL PIPELINE via SENDGRID (get -> render -> send)");
        Console.WriteLine(" 5. FULL PIPELINE via MANDRILL (get -> server-side merge render -> send)");
        Console.WriteLine(" 6. SEND via MANDRILL TEMPLATE  (send-template by template name)");
        Console.WriteLine(" 7. Exit");
        Console.Write("Select: ");
    }

    private static string Describe(string? secret) =>
        string.IsNullOrWhiteSpace(secret) ? "(NOT CONFIGURED)" : $"{secret[..6]}...{secret[^4..]}";

    private static async Task<int> SelfTestAsync(PocSettings settings, JsonLinesLogger log)
    {
        Console.WriteLine("--- Self test ---");
        Console.WriteLine($"Mailchimp API key configured : {!string.IsNullOrWhiteSpace(settings.MailchimpApiKey)}");
        Console.WriteLine($"SendGrid API key configured  : {!string.IsNullOrWhiteSpace(settings.SendGridApiKey)}");
        Console.WriteLine($"Recipient configured         : {!string.IsNullOrWhiteSpace(settings.ToEmail)}");

        var rendered = TemplateRenderer.Render(TemplateRenderer.SampleTemplateHtml, TemplateRenderer.SampleMergeData);
        Console.WriteLine($"Offline merge render works   : {rendered.Contains("Jane", StringComparison.Ordinal)}");
        await log.WriteAsync("selftest", "offline-render", "success", 0);

        if (!string.IsNullOrWhiteSpace(settings.MandrillApiKey))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var mandrill = new MandrillApiClient(settings.MandrillApiKey);
            var ping = await mandrill.PingAsync();
            Console.WriteLine($"Mandrill ping                : {(ping.IsSuccess ? "PONG (key valid)" : $"FAILED HTTP {(int)ping.StatusCode}")} ({sw.ElapsedMilliseconds} ms)");
            await log.WriteAsync("selftest", "mandrill-ping", ping.IsSuccess ? "success" : $"http-{(int)ping.StatusCode}", sw.ElapsedMilliseconds, ping.IsSuccess ? null : ping.Body);
        }

        Console.WriteLine("Self test complete. Add API keys via 'dotnet user-secrets' to run live operations.");
        return 0;
    }

    private static async Task ListTemplatesAsync(PocSettings settings, JsonLinesLogger log)
    {
        if (!EnsureConfigured(settings.MailchimpApiKey, "Mailchimp:ApiKey")) return;
        var client = new MailchimpApiClient(settings.MailchimpApiKey!);
        var (ok, status, body) = await client.GetAsyncAsync("/templates?count=100");
        Console.WriteLine($"HTTP {(int)status} {status}");
        if (!ok)
        {
            Console.WriteLine(body);
            return;
        }

        using var doc = JsonDocument.Parse(body);
        foreach (var t in doc.RootElement.GetProperty("templates").EnumerateArray())
        {
            var id = t.GetProperty("id").GetString();
            var name = t.TryGetProperty("name", out var n) ? n.GetString() : "(unnamed)";
            Console.WriteLine($"  {id,-40} {name}");
        }
        Console.WriteLine("Copy the template id you need for option 2 / the full pipeline.");
    }

    private static async Task GetTemplateAsync(PocSettings settings, JsonLinesLogger log)
    {
        if (!EnsureConfigured(settings.MailchimpApiKey, "Mailchimp:ApiKey")) return;
        Console.Write("Template id: ");
        var id = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(id)) return;

        var client = new MailchimpApiClient(settings.MailchimpApiKey!);
        var (ok, status, body) = await client.GetAsyncAsync($"/templates/{Uri.EscapeDataString(id)}");
        Console.WriteLine($"HTTP {(int)status} {status}");
        if (!ok)
        {
            Console.WriteLine(body);
            return;
        }

        using var doc = JsonDocument.Parse(body);
        var html = doc.RootElement.TryGetProperty("html", out var h) ? h.GetString() ?? string.Empty : string.Empty;
        var path = Path.Combine(settings.LogsDirectory, $"template-{Sanitise(id)}.html");
        await File.WriteAllTextAsync(path, html);
        Console.WriteLine($"HTML length : {html.Length}");
        Console.WriteLine($"Saved to    : {path}");
        Console.WriteLine("Preview     : " + html[..Math.Min(html.Length, 500)]);
    }

    private static void RenderSample()
    {
        var rendered = TemplateRenderer.Render(TemplateRenderer.SampleTemplateHtml, TemplateRenderer.SampleMergeData);
        Console.WriteLine("--- Rendered sample (first 800 chars) ---");
        Console.WriteLine(rendered[..Math.Min(rendered.Length, 800)]);
        Console.WriteLine();
        Console.WriteLine("Note: authoritative merge rendering happens in Mandrill at send time;");
        Console.WriteLine("this is a client-side preview demonstrating tag semantics only.");
    }

    // Template 10128760 "Assessment Booked" stores its content in API-exposed default-content
    // sections (header/main/footer) and is the template the download-and-send pipeline can use.
    // Template 10128764 "Assessment Booking Confirmation" exposes no content via the API
    // (empty default-content), so it cannot be downloaded.
    private const string AssessmentBookedTemplateId = "10128760";

    private static async Task FullPipelineAsync(PocSettings settings, JsonLinesLogger log, string templateId = null)
    {
        if (!EnsureConfigured(settings.MailchimpApiKey, "Mailchimp:ApiKey")) return;
        if (!EnsureConfigured(settings.SendGridApiKey, "SendGrid:ApiKey")) return;
        if (!EnsureConfigured(settings.ToEmail, "Poc:ToEmail")) return;

        var id = templateId ?? PromptTemplateId();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string html;
        string subject;

        if (string.IsNullOrEmpty(id))
        {
            subject = "[POC] Assessment Booked (SendGrid)";
            html = TemplateRenderer.SampleTemplateHtml;
        }
        else
        {
            var mc = new MailchimpApiClient(settings.MailchimpApiKey!);
            var (ok, status, body) = await mc.GetAsyncAsync($"/templates/{Uri.EscapeDataString(id)}");
            Console.WriteLine($"Mailchimp HTTP {(int)status} in {sw.ElapsedMilliseconds} ms");
            await log.WriteAsync("get-template", id, ok ? "success" : $"http-{(int)status}", sw.ElapsedMilliseconds, ok ? null : body);
            if (!ok)
            {
                Console.WriteLine(body);
                return;
            }
            using var doc = JsonDocument.Parse(body);
            subject = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? subjectFallback : subjectFallback;
            // Try to get HTML from default-content sections (this account's storage format)
            html = await FetchTemplateHtmlFromDefaultContent(mc, id);
            if (string.IsNullOrEmpty(html))
            {
                Console.WriteLine("Template HTML not returned by API. Using Assessment Booked sample.");
                html = TemplateRenderer.AssessmentBookedSampleHtml;
            }
        }

        var sender = new SendGridApiClient(settings.SendGridApiKey!);
        var mergeData = string.Equals(id, AssessmentBookedTemplateId, StringComparison.Ordinal)
            ? TemplateRenderer.AssessmentBookedMergeData
            : TemplateRenderer.SampleMergeData;
        var rendered = TemplateRenderer.Render(html, mergeData);
        sw.Restart();
        var sent = await sender.SendHtmlAsync(subject, rendered, settings.ToEmail!, settings.FromEmail!);
        Console.WriteLine($"SendGrid send: {(sent.IsSuccess ? "SUCCESS" : $"FAILED {(int)sent.StatusCode}")} in {sw.ElapsedMilliseconds} ms");
        if (!sent.IsSuccess) Console.WriteLine(sent.ErrorBody);
        await log.WriteAsync("send-via-sendgrid", subject, sent.IsSuccess ? "success" : $"http-{(int)sent.StatusCode}", sw.ElapsedMilliseconds, sent.ErrorBody);
    }

    private static async Task FullPipelineViaMandrillAsync(PocSettings settings, JsonLinesLogger log, string templateId = null)
    {
        if (!EnsureConfigured(settings.MailchimpApiKey, "Mailchimp:ApiKey")) return;
        if (!EnsureConfigured(settings.MandrillApiKey, "Mandrill:ApiKey")) return;
        if (!EnsureConfigured(settings.ToEmail, "Poc:ToEmail")) return;

        var id = templateId ?? PromptTemplateId();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string html;
        string subject;

        if (string.IsNullOrEmpty(id))
        {
            subject = "[POC] Assessment Booked (Mandrill)";
            html = TemplateRenderer.SampleTemplateHtml;
        }
        else
        {
            var mc = new MailchimpApiClient(settings.MailchimpApiKey!);
            var (ok, status, body) = await mc.GetAsyncAsync($"/templates/{Uri.EscapeDataString(id)}");
            Console.WriteLine($"Mailchimp HTTP {(int)status} in {sw.ElapsedMilliseconds} ms");
            await log.WriteAsync("get-template", id, ok ? "success" : $"http-{(int)status}", sw.ElapsedMilliseconds, ok ? null : body);
            if (!ok)
            {
                Console.WriteLine(body);
                return;
            }
            using var doc = JsonDocument.Parse(body);
            subject = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? subjectFallback : subjectFallback;
            html = await FetchTemplateHtmlFromDefaultContent(mc, id);
            if (string.IsNullOrEmpty(html))
            {
                Console.WriteLine("Template HTML not returned by API. Using Assessment Booked sample.");
                html = TemplateRenderer.AssessmentBookedSampleHtml;
            }
        }

        var templateName = string.IsNullOrEmpty(id) ? "Sample template" : subject;
        Console.WriteLine($"Using template: {templateName}");
        Console.WriteLine($"From: {settings.FromEmail}  To: {settings.ToEmail}");
        Console.WriteLine($"Using template: {templateName}");
        Console.WriteLine($"From: {settings.FromEmail}  To: {settings.ToEmail}");

        // Deliberately NOT pre-rendered: Mandrill renders *|TAG|* server-side (merge_language=mailchimp),
        // demonstrating the true Option 2 flow where retrieved HTML goes out with data attached.
        var mandrill = new MandrillApiClient(settings.MandrillApiKey!);
        var mergeData = string.Equals(id, AssessmentBookedTemplateId, StringComparison.Ordinal)
            ? TemplateRenderer.AssessmentBookedMergeData
            : TemplateRenderer.SampleMergeData;
        sw.Restart();
        var sent = await mandrill.SendHtmlAsync(
            settings.FromEmail!,
            "Transactional Email POC",
            subject,
            html,
            settings.ToEmail!,
            mergeData);
        Console.WriteLine($"Mandrill send: {(sent.IsSuccess ? $"SUCCESS ({sent.Status})" : $"FAILED ({sent.Status})")} in {sw.ElapsedMilliseconds} ms");
        if (!sent.IsSuccess)
        {
            Console.WriteLine($"Reject reason: {sent.RejectReason}");
            Console.WriteLine("Hint: demo tier only delivers to recipients at an AUTHENTICATED domain (SPF/DKIM).");
        }
        await log.WriteAsync("send-via-mandrill", subject, sent.IsSuccess ? sent.Status : $"rejected:{sent.RejectReason}", sw.ElapsedMilliseconds, sent.RejectReason);
    }

    private const string subjectFallback = "[POC] Mailchimp template";

    private static bool EnsureConfigured(string? value, string settingKey)
    {
        if (!string.IsNullOrWhiteSpace(value)) return true;
        Console.WriteLine($"Missing configuration '{settingKey}'. Set it via:");
        Console.WriteLine($"  dotnet user-secrets set \"{settingKey}\" \"<value>\" --project MailchimpPoc.csproj");
        return false;
    }

    private static string PromptTemplateId()
    {
        Console.Write("Template id (blank = built-in sample): ");
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Reads an optional template id passed after --sendgrid or --mandrill, e.g. "--mandrill 10128760".
    /// This lets the same binary target any of the templates users create in Mailchimp without code changes.
    /// </summary>
    private static string? ExtractTemplateId(string[] args) =>
        ExtractArgValue(args, "--sendgrid") ?? ExtractArgValue(args, "--mandrill");

    /// <summary>Returns the value that follows a named CLI flag, or null when the flag has no value.</summary>
    private static string? ExtractArgValue(string[] args, string flag)
    {
        var index = Array.FindIndex(args, a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));
        if (index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith('-'))
            return args[index + 1];
        return null;
    }

    /// <summary>
    /// Sends using a template stored in the Mandrill (Mailchimp Transactional) template library,
    /// referenced by its name/slug. This is the recommended production path: the template lives
    /// in Mailchimp, the app only points to it and passes merge variables.
    /// </summary>
    private static async Task MandrillTemplatePipelineAsync(PocSettings settings, JsonLinesLogger log, string templateName)
    {
        if (!EnsureConfigured(settings.MandrillApiKey, "Mandrill:ApiKey")) return;
        if (!EnsureConfigured(settings.ToEmail, "Poc:ToEmail")) return;

        var mandrill = new MandrillApiClient(settings.MandrillApiKey!);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sent = await mandrill.SendTemplateAsync(
            templateName,
            settings.FromEmail!,
            "Transactional Email POC",
            "Assessment Booked (Mandrill template)",
            settings.ToEmail!,
            TemplateRenderer.AssessmentBookedMergeData);
        Console.WriteLine($"Mandrill send-template: {(sent.IsSuccess ? $"SUCCESS ({sent.Status})" : $"FAILED ({sent.Status})")}");
        if (!sent.IsSuccess) Console.WriteLine(sent.RejectReason);
        await log.WriteAsync("send-template-via-mandrill", templateName, sent.IsSuccess ? sent.Status : $"rejected:{sent.RejectReason}", sw.ElapsedMilliseconds, sent.RejectReason);
    }

    private static async Task MandrillTemplateByNameAsync(PocSettings settings, JsonLinesLogger log)
    {
        Console.Write("Mandrill template name (e.g. poc-dummy-code): ");
        var name = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(name)) return;
        await MandrillTemplatePipelineAsync(settings, log, name);
    }

    /// <summary>
    /// Fetches template content from Mailchimp's /default-content endpoint and assembles
    /// header + main + footer sections into a single HTML document.
    /// This is how this Mailchimp account stores template content.
    /// </summary>
    private static async Task<string> FetchTemplateHtmlFromDefaultContent(MailchimpApiClient mc, string templateId)
    {
        var (ok, status, body) = await mc.GetAsyncAsync($"/templates/{Uri.EscapeDataString(templateId)}/default-content");
        if (!ok)
        {
            Console.WriteLine($"Default-content fetch failed: HTTP {(int)status}");
            return null;
        }
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("sections", out var sections))
            return null;

        var sb = new StringBuilder();
        var htmlStart = """
        <!DOCTYPE html>
        <html>
          <body style="font-family: Arial, sans-serif; color:#222; line-height:1.5;">
            <div style="max-width:600px; margin:0 auto; padding:20px;">
        """;
        var htmlEnd = """
            </div>
          </body>
        </html>
        """;

        sb.Append(htmlStart);
        // Common section order: header, preheader, main, footer, etc.
        var sectionOrder = new[] { "header", "preheader", "main", "footer", "tracking_information", "prex_headline" };
        foreach (var section in sectionOrder)
        {
            if (sections.TryGetProperty(section, out var content) && content.ValueKind == JsonValueKind.String)
            {
                var html = content.GetString();
                if (!string.IsNullOrWhiteSpace(html))
                    sb.Append(html);
            }
        }
        // Also include any other sections not in the standard order
        foreach (var prop in sections.EnumerateObject())
        {
            if (!sectionOrder.Contains(prop.Name, StringComparer.OrdinalIgnoreCase) &&
                prop.Value.ValueKind == JsonValueKind.String)
            {
                var html = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(html))
                    sb.Append(html);
            }
        }
        sb.Append(htmlEnd);
        return sb.ToString();
    }

    private static string Sanitise(string id) => new(id.Where(char.IsLetterOrDigit).ToArray());
}
