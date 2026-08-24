namespace EmailCentral.Api.Domain;

public sealed record Recipient(string Email, string? Name = null);

public sealed record SendEmailRequest(
    string TemplateKey,
    IReadOnlyList<Recipient> To,
    IReadOnlyDictionary<string, object?>? Data,
    string? SourceSystem,
    string? IdempotencyKey);

public sealed record SendEmailResponse(
    string MessageId,
    string Status,
    string TemplateKey,
    string Provider);

public sealed record ActivityEntry(
    DateTimeOffset Timestamp,
    string Type,
    string TemplateKey,
    string? MessageId,
    string SourceSystem,
    string Status,
    string? Detail);
