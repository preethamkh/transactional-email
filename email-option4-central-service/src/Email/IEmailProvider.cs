using EmailCentral.Api.Domain;

namespace EmailCentral.Api.Email;

public sealed record Branding(string FromEmail, string FromName);

public sealed record OutgoingEmail(
    string ProviderTemplateId,
    Branding From,
    IReadOnlyList<Recipient> To,
    string? Subject,
    IReadOnlyDictionary<string, object?>? TemplateData,
    IReadOnlyDictionary<string, string>? CustomArgs);

public sealed record ProviderSendResult(bool IsSuccess, string? MessageId, string? Error);

public interface IEmailProvider
{
    string Name { get; }

    Task<ProviderSendResult> SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default);
}
