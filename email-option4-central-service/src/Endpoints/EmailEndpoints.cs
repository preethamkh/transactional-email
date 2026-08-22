using EmailCentral.Api.Domain;
using EmailCentral.Api.Email;
using EmailCentral.Api.Logging;
using EmailCentral.Api.Templates;

namespace EmailCentral.Api.Endpoints;

/// <summary>Central email API endpoints. The single HTTP surface every system calls (BRD FR-004).</summary>
public static class EmailEndpoints
{
    public static IEndpointRouteBuilder MapEmailEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/email/send", SendAsync)
            .WithName("SendEmail")
            .WithSummary("Send a templated email via the configured provider");

        app.MapGet("/api/v1/templates", ListTemplates)
            .WithName("ListTemplates");

        app.MapGet("/api/v1/templates/{key}/preview", PreviewAsync)
            .WithName("PreviewTemplate");

        app.MapGet("/api/v1/activity", QueryActivity)
            .WithName("QueryActivity");

        return app;
    }

    internal static async Task<IResult> SendAsync(
        SendEmailRequest request,
        TemplateRegistry registry,
        IEmailProvider provider,
        ActivityLog activityLog,
        HttpContext httpContext)
    {
        if (!registry.TryGetTemplate(request.TemplateKey, out var template))
        {
            await LogAsync(activityLog, "send", request, null, "rejected-unknown-template", $"Unknown templateKey '{request.TemplateKey}'");
            return TypedResults.NotFound(new { error = $"Unknown templateKey '{request.TemplateKey}'." });
        }

        if (!registry.TryGetBranding(template.DefaultBranding, out var branding))
        {
            await LogAsync(activityLog, "send", request, null, "rejected-unknown-branding", $"Branding '{template.DefaultBranding}' not found");
            return TypedResults.Problem($"Branding '{template.DefaultBranding}' is not configured.");
        }

        var sourceSystem = request.SourceSystem ?? httpContext.Request.Headers["X-Source-System"].ToString();
        var messageId = Guid.NewGuid().ToString("N");
        var subject = request.Data is not null && request.Data.TryGetValue("subject", out var s) ? s?.ToString() : template.Name;

        var outgoing = new OutgoingEmail(
            template.ProviderTemplateId,
            new Branding(branding.FromEmail, branding.FromName),
            request.To,
            subject,
            request.Data,
            new Dictionary<string, string>
            {
                ["central_message_id"] = messageId,
                ["template_key"] = template.Key,
                ["source_system"] = sourceSystem
            });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await provider.SendAsync(outgoing);
        sw.Stop();

        var status = result.IsSuccess ? "accepted" : "provider-error";
        await activityLog.AppendAsync(new ActivityEntry(
            DateTimeOffset.UtcNow, "send", template.Key, messageId, sourceSystem, status,
            $"{result.Error} ({sw.ElapsedMilliseconds}ms)".Trim()));

        return result.IsSuccess
            ? TypedResults.Accepted($"/api/v1/activity", new SendEmailResponse(messageId, status, template.Key, provider.Name))
            : TypedResults.Problem(new global::Microsoft.AspNetCore.Mvc.ProblemDetails
              {
                  Title = "Provider send failed",
                  Detail = result.Error,
                  Status = StatusCodes.Status502BadGateway
              });
    }

    internal static IResult ListTemplates(TemplateRegistry registry) =>
        TypedResults.Ok(registry.All);

    internal static IResult PreviewAsync(string key, TemplateRegistry registry, string? data)
    {
        if (!registry.TryGetTemplate(key, out var template))
        {
            return TypedResults.NotFound(new { error = $"Unknown templateKey '{key}'." });
        }

        IReadOnlyDictionary<string, object?>? sampleData = null;
        if (!string.IsNullOrWhiteSpace(data))
        {
            try
            {
                sampleData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(data);
            }
            catch (System.Text.Json.JsonException)
            {
                return TypedResults.BadRequest(new { error = "'data' must be a JSON object, e.g. ?data={\"firstName\":\"Jane\"}" });
            }
        }

        // POC preview uses an indicative inline sample; production would fetch live template content from the provider.
        var html = $"<p>Hello {{{{firstName}}}},</p><p>This is a preview of template '{key}'.</p>";
        return TypedResults.Ok(new
        {
            template.Key,
            template.ProviderTemplateId,
            previewHtml = PreviewRenderer.Render(html, sampleData),
            note = "Indicative render only; authoritative rendering happens in the provider at send time."
        });
    }

    internal static IResult QueryActivity(ActivityLog activityLog, int take = 20) =>
        TypedResults.Ok(activityLog.Query(take));

    private static Task LogAsync(
        ActivityLog activityLog, string type, SendEmailRequest request, string? messageId, string status, string detail) =>
        activityLog.AppendAsync(new ActivityEntry(
            DateTimeOffset.UtcNow, type, request.TemplateKey, messageId,
            request.SourceSystem ?? "(unspecified)", status, detail));
}

public static class ActivityLogExtensions
{
    public static Task AppendAsync(this ActivityLog log, ActivityEntry entry)
    {
        log.Append(entry);
        return Task.CompletedTask;
    }
}
