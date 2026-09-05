using System.Text.Json;

namespace EmailCentral.Api.Templates;

public sealed record BrandingConfig(string FromEmail, string FromName);

public sealed record EmailTemplate(
    string Key,
    string Name,
    string Provider,
    string ProviderTemplateId,
    string Owner,
    string DefaultBranding);

public sealed class TemplateRegistry
{
    private readonly IReadOnlyDictionary<string, EmailTemplate> _templates;
    private readonly IReadOnlyDictionary<string, BrandingConfig> _brandings;

    public TemplateRegistry(string filePath)
    {
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        _brandings = root.GetProperty("brandings").EnumerateObject()
            .ToDictionary(
                b => b.Name,
                b => new BrandingConfig(
                    b.Value.GetProperty("fromEmail").GetString() ?? string.Empty,
                    b.Value.GetProperty("fromName").GetString() ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

        _templates = root.GetProperty("templates").EnumerateArray()
            .ToDictionary(
                t => t.GetProperty("key").GetString() ?? string.Empty,
                t => new EmailTemplate(
                    t.GetProperty("key").GetString() ?? string.Empty,
                    t.GetProperty("name").GetString() ?? string.Empty,
                    t.GetProperty("provider").GetString() ?? string.Empty,
                    t.GetProperty("providerTemplateId").GetString() ?? string.Empty,
                    t.GetProperty("owner").GetString() ?? string.Empty,
                    t.GetProperty("defaultBranding").GetString() ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetTemplate(string key, out EmailTemplate template) => _templates.TryGetValue(key, out template!);

    public bool TryGetBranding(string name, out BrandingConfig branding) => _brandings.TryGetValue(name, out branding!);

    public IReadOnlyCollection<EmailTemplate> All => _templates.Values.ToArray();
}
