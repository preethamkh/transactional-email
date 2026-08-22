using EmailCentral.Api.Templates;
using Xunit;

namespace EmailCentral.Tests;

public class TemplateRegistryTests : IDisposable
{
    private readonly string _tempFilePath = Path.Combine(Path.GetTempPath(), $"registry-{Guid.NewGuid():N}.json");

    public TemplateRegistryTests()
    {
        File.WriteAllText(_tempFilePath, SampleJson);
    }

    [Fact]
    public void TryGetTemplate_ResolvesByKey()
    {
        var registry = new TemplateRegistry(_tempFilePath);

        Assert.True(registry.TryGetTemplate("PasswordReset", out var template));
        Assert.Equal("d-123", template.ProviderTemplateId);
        Assert.Equal("Engagement", template.Owner);
    }

    [Fact]
    public void TryGetTemplate_IsCaseInsensitive()
    {
        var registry = new TemplateRegistry(_tempFilePath);

        Assert.True(registry.TryGetTemplate("passwordreset", out var template));
        Assert.Equal("PasswordReset", template.Key);
    }

    [Fact]
    public void TryGetTemplate_RejectsUnknownKey()
    {
        var registry = new TemplateRegistry(_tempFilePath);

        Assert.False(registry.TryGetTemplate("Nope", out _));
    }

    [Fact]
    public void TryGetBranding_ResolvesDefaultBranding()
    {
        var registry = new TemplateRegistry(_tempFilePath);

        Assert.True(registry.TryGetBranding("apc", out var branding));
        Assert.Equal("no-reply@apc.example.org", branding.FromEmail);
    }

    [Fact]
    public void All_ReturnsCatalogue()
    {
        Assert.Equal(2, new TemplateRegistry(_tempFilePath).All.Count);
    }

    private const string SampleJson = """
        {
          "brandings": { "apc": { "fromEmail": "no-reply@apc.example.org", "fromName": "APC" } },
          "templates": [
            { "key": "PasswordReset", "name": "Password reset", "provider": "sendgrid",
              "providerTemplateId": "d-123", "owner": "Engagement", "defaultBranding": "apc" },
            { "key": "Welcome", "name": "Welcome", "provider": "sendgrid",
              "providerTemplateId": "d-456", "owner": "Engagement", "defaultBranding": "apc" }
          ]
        }
        """;

    public void Dispose() => File.Delete(_tempFilePath);
}
