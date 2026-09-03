using System.Collections.Concurrent;
using System.Text.Json;
using Apc.Email.Contracts;
using Apc.Email.Mandrill;
using Azure.Messaging.ServiceBus;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new ConcurrentBag<EmailAuditRecord>());
var serviceBusConnection = builder.Configuration["ServiceBusConnection"];
var auditQueue = builder.Configuration["EmailAuditQueue"] ?? "email-events";
if (!string.IsNullOrWhiteSpace(serviceBusConnection))
    builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnection));
var app = builder.Build();

var mandrillKey = builder.Configuration["Mandrill:ApiKey"] ?? Environment.GetEnvironmentVariable("MANDRILL_API_KEY");
var fromEmail = builder.Configuration["Mandrill:FromEmail"] ?? Environment.GetEnvironmentVariable("FROM_EMAIL") ?? "info@physiocouncil.com.au";
var callerKey = builder.Configuration["ApiKeys:demo"] ?? Environment.GetEnvironmentVariable("DEMO_API_KEY") ?? "demo-key";

app.MapGet("/health", () => Results.Ok(new { status = "ok", provider = "mandrill", mode = string.IsNullOrWhiteSpace(mandrillKey) ? "simulation" : "live" }));
app.MapGet("/", () => Results.Content(SupportPage(callerKey), "text/html"));

app.MapPost("/api/v1/email/send", async (HttpRequest httpRequest, EmailRequest request, ConcurrentBag<EmailAuditRecord> audit, IServiceProvider services) =>
{
    if (httpRequest.Headers["X-Api-Key"] != callerKey)
        return Results.Unauthorized();
    var serviceBusClient = services.GetService<ServiceBusClient>();
    if (request.To.Count == 0 || string.IsNullOrWhiteSpace(request.TemplateKey))
        return Results.BadRequest(new { error = "TemplateKey and at least one recipient are required." });

    var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString("N");
    EmailSendResult result;
    if (string.IsNullOrWhiteSpace(mandrillKey))
    {
        result = new EmailSendResult(true, "simulated", correlationId, $"demo-{correlationId[..8]}", request.SourceSystem, request.TemplateKey);
    }
    else
    {
        using var httpClient = new HttpClient();
        var sender = new MandrillEmailSender(httpClient, mandrillKey, fromEmail);
        result = await sender.SendTemplateAsync(request with { CorrelationId = correlationId }, request.TemplateKey);
    }

    foreach (var recipient in request.To)
    {
        var auditRecord = new EmailAuditRecord(DateTimeOffset.UtcNow, result.CorrelationId, request.SourceSystem,
            request.TemplateKey, recipient.Email, result.Status, result.ProviderMessageId, result.Error, request.Data);
        audit.Add(auditRecord);
        if (serviceBusClient is not null)
        {
            await using var sender = serviceBusClient.CreateSender(auditQueue);
            await sender.SendMessageAsync(new ServiceBusMessage(JsonSerializer.Serialize(auditRecord))
            {
                ContentType = "application/json",
                CorrelationId = result.CorrelationId
            });
        }
    }

    return Results.Accepted($"/api/v1/activity/{result.CorrelationId}", result);
});

app.MapGet("/api/v1/activity", (string? search, string? status, ConcurrentBag<EmailAuditRecord> audit) =>
{
    var results = audit.Where(item =>
        (string.IsNullOrWhiteSpace(search) || JsonSerializer.Serialize(item).Contains(search, StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(status) || item.Status.Equals(status, StringComparison.OrdinalIgnoreCase)))
        .OrderByDescending(item => item.OccurredAt);
    return Results.Ok(new { count = results.Count(), items = results });
}).AddEndpointFilter((context, next) =>
{
    var request = context.HttpContext.Request;
    return request.Headers["X-Api-Key"] != callerKey
        ? ValueTask.FromResult<object?>(Results.Unauthorized())
        : next(context);
});

app.MapGet("/api/v1/templates", (HttpRequest request) =>
{
    if (request.Headers["X-Api-Key"] != callerKey)
        return Results.Unauthorized();
    return Results.Ok(new[]
    {
        new { key = "AssessmentBooked", slug = "assessment-booked", owner = "Assessment", provider = "Mandrill" },
        new { key = "Welcome", slug = "welcome", owner = "Engagement", provider = "Mandrill" }
    });
});

app.MapPost("/api/v1/events/mandrill", (JsonElement events) => Results.Ok(new { received = events.ValueKind == JsonValueKind.Array }));
app.Run();

static string SupportPage(string callerKey) => """
<!doctype html><html><head><meta charset="utf-8"><title>APC Email Audit Demo</title>
<style>body{font:16px system-ui;max-width:1100px;margin:40px auto;padding:0 20px;color:#17202a}button{padding:9px 14px}input{padding:9px;margin-right:8px}table{border-collapse:collapse;width:100%;margin-top:20px}th,td{border-bottom:1px solid #ddd;text-align:left;padding:10px}.tag{font-weight:600;color:#126b45}</style></head>
<body><h1>APC Transactional Email Audit</h1><p>Search the long-lived audit record, not the provider's short retention window.</p>
<p><input id="search" placeholder="recipient, template, correlation ID"><input id="status" placeholder="status"><button onclick="load()">Search</button></p>
<table><thead><tr><th>Time</th><th>Source</th><th>Template</th><th>Recipient</th><th>Status</th><th>Correlation</th></tr></thead><tbody id="rows"></tbody></table>
<script>async function load(){const q=new URLSearchParams();if(search.value)q.set('search',search.value);if(status.value)q.set('status',status.value);const r=await fetch('/api/v1/activity?'+q,{headers:{'X-Api-Key':'__DEMO_KEY__'}});const d=await r.json();rows.innerHTML=d.items.map(x=>`<tr><td>${x.occurredAt}</td><td>${x.sourceSystem}</td><td>${x.templateKey}</td><td>${x.recipient}</td><td class="tag">${x.status}</td><td>${x.correlationId}</td></tr>`).join('')}load()</script></body></html>
""".Replace("__DEMO_KEY__", callerKey);
