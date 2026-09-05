using Microsoft.Extensions.Configuration;

namespace MailchimpPoc;

public sealed record PocSettings(
    string? MailchimpApiKey,
    string? SendGridApiKey,
    string? MandrillApiKey,
    string? FromEmail,
    string? ToEmail,
    string LogsDirectory)
{
    public static PocSettings FromConfiguration(IConfiguration configuration)
    {
        var logsDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDirectory);
        var fromEmail = configuration[SettingKeys.FromEmail]
            ?? configuration[SettingKeys.ToEmail]
            ?? "no-reply@transactional-dev.example.com";
        return new PocSettings(
            configuration[SettingKeys.MailchimpApiKey],
            configuration[SettingKeys.SendGridApiKey],
            configuration[SettingKeys.MandrillApiKey],
            fromEmail,
            configuration[SettingKeys.ToEmail],
            logsDirectory);
    }
}

public static class SettingKeys
{
    public const string MailchimpApiKey = "Mailchimp:ApiKey";
    public const string SendGridApiKey = "SendGrid:ApiKey";
    public const string MandrillApiKey = "Mandrill:ApiKey";
    public const string FromEmail = "Poc:FromEmail";
    public const string ToEmail = "Poc:ToEmail";
}
