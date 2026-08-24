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

        if (args.Any(a => a.Equals("--sendgrid", StringComparison.OrdinalIgnoreCase)))
        {
            await FullPipelineAsync(settings, log);
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
                    case "6": return 0;
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
        Console.WriteLine();
        Console.WriteLine(" 1. List templates            (Mailchimp API)");
        Console.WriteLine(" 2. Get template HTML by ID   (saved to logs/)");
        Console.WriteLine(" 3. Render sample merge tags  (offline proof)");
        Console.WriteLine(" 4. FULL PIPELINE via SENDGRID (get -> render -> send)");
        Console.WriteLine(" 5. FULL PIPELINE via MANDRILL (get -> server-side merge render -> send)");
        Console.WriteLine(" 6. Exit");
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

    private static async Task FullPipelineAsync(PocSettings settings, JsonLinesLogger log)
    {
        if (!EnsureConfigured(settings.MailchimpApiKey, "Mailchimp:ApiKey")) return;
        if (!EnsureConfigured(settings.SendGridApiKey, "SendGrid:ApiKey")) return;
        if (!EnsureConfigured(settings.ToEmail, "Poc:ToEmail")) return;

        Console.Write("Template id (blank = built-in sample): ");
        var id = Console.ReadLine()?.Trim();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        string html;
        string subject;

        if (string.IsNullOrEmpty(id))
        {
            subject = "[POC] Sample template";
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
            html = doc.RootElement.TryGetProperty("html", out var h) ? h.GetString() ?? string.Empty : string.Empty;
            subject = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? subjectFallback : subjectFallback;
        }

        var sender = new SendGridApiClient(settings.SendGridApiKey!);
        var rendered = TemplateRenderer.Render(html, TemplateRenderer.SampleMergeData);
        sw.Restart();
        var sent = await sender.SendHtmlAsync(subject, rendered, settings.ToEmail!);
        Console.WriteLine($"SendGrid send: {(sent.IsSuccess ? "SUCCESS" : $"FAILED {(int)sent.StatusCode}")} in {sw.ElapsedMilliseconds} ms");
        if (!sent.IsSuccess) Console.WriteLine(sent.ErrorBody);
        await log.WriteAsync("send-via-sendgrid", subject, sent.IsSuccess ? "success" : $"http-{(int)sent.StatusCode}", sw.ElapsedMilliseconds, sent.ErrorBody);
    }

    private static async Task FullPipelineViaMandrillAsync(PocSettings settings, JsonLinesLogger log)
    {
        if (!EnsureConfigured(settings.MailchimpApiKey, "Mailchimp:ApiKey")) return;
        if (!EnsureConfigured(settings.MandrillApiKey, "Mandrill:ApiKey")) return;
        if (!EnsureConfigured(settings.ToEmail, "Poc:ToEmail")) return;

        Console.Write("Template id (blank = built-in sample): ");
        var id = Console.ReadLine()?.Trim();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        string html;
        string subject;

        if (string.IsNullOrEmpty(id))
        {
            subject = "[POC] Sample template (Mandrill)";
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
            html = doc.RootElement.TryGetProperty("html", out var h) ? h.GetString() ?? string.Empty : string.Empty;
            subject = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? subjectFallback : subjectFallback;
        }

        // Deliberately NOT pre-rendered: Mandrill renders *|TAG|* server-side (merge_language=mailchimp),
        // demonstrating the true Option 2 flow where retrieved HTML goes out with data attached.
        var mandrill = new MandrillApiClient(settings.MandrillApiKey!);
        sw.Restart();
        var sent = await mandrill.SendHtmlAsync(settings.ToEmail!, "Transactional Email POC", subject, html, settings.ToEmail!, TemplateRenderer.SampleMergeData);
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

    private static string Sanitise(string id) => new(id.Where(char.IsLetterOrDigit).ToArray());
}
