using Microsoft.Extensions.Configuration;

namespace MailchimpPoc;

public sealed record PocSettings(
    string? MailchimpApiKey,
    string? SendGridApiKey,
    string? ToEmail,
    string LogsDirectory)
{
    public static PocSettings FromConfiguration(IConfiguration configuration)
    {
        var logsDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDirectory);
        return new PocSettings(
            configuration[SettingKeys.MailchimpApiKey],
            configuration[SettingKeys.SendGridApiKey],
            configuration[SettingKeys.ToEmail],
            logsDirectory);
    }
}

public static class SettingKeys
{
    public const string MailchimpApiKey = "Mailchimp:ApiKey";
    public const string SendGridApiKey = "SendGrid:ApiKey";
    public const string ToEmail = "Poc:ToEmail";
}
