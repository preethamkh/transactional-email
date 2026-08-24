using System.Text.Json;
using EmailCentral.Api.Auth;
using EmailCentral.Api.Domain;
using EmailCentral.Api.Email;
using EmailCentral.Api.Endpoints;
using EmailCentral.Api.Logging;
using EmailCentral.Api.Templates;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton(_ => new TemplateRegistry(
    Path.Combine(AppContext.BaseDirectory, "templates.json")));
builder.Services.AddSingleton(_ => new ActivityLog(
    Path.Combine(AppContext.BaseDirectory, "logs")));
builder.Services.AddSingleton<IEmailProvider>(sp => new SendGridProvider(
    builder.Configuration["SendGrid:ApiKey"],
    sp.GetRequiredService<ActivityLog>()));

var app = builder.Build();

app.MapOpenApi();

app.MapGet("/health", (IEmailProvider provider) =>
    Results.Ok(new { status = "ok", provider = provider.Name, time = DateTimeOffset.UtcNow }));

app.UseMiddleware<ApiKeyMiddleware>();

app.MapEmailEndpoints();

// SendGrid event webhook receiver: appends delivery/open/click/bounce events to the activity log.
app.MapPost("/api/v1/events/sendgrid", async (JsonElement events, ActivityLog activityLog) =>
{
    if (events.ValueKind is JsonValueKind.Array)
    {
        foreach (var e in events.EnumerateArray())
        {
            var type = e.TryGetProperty("event", out var ev) ? ev.GetString() : null;
            var email = e.TryGetProperty("email", out var em) ? em.GetString() : null;
            var sgMessageId = e.TryGetProperty("sg_message_id", out var mid) ? mid.GetString() : null;

            await activityLog.AppendAsync(new ActivityEntry(
                DateTimeOffset.UtcNow, "event", "(provider-event)", sgMessageId,
                "sendgrid-webhook", type ?? "unknown", $"recipient={email}"));
        }
    }
    return Results.Ok(new { received = true });
});

app.Run();
