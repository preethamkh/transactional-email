using EmailCentral.Api.Auth;
using EmailCentral.Api.Email;
using EmailCentral.Api.Domain;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Xunit;

namespace EmailCentral.Tests;

public class ApiKeyValidatorTests
{
    private static IConfiguration BuildConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiKeys:assessment-portal"] = "secret-001"
        })
        .Build();

    [Theory]
    [InlineData("assessment-portal", "secret-001", true)]
    [InlineData("assessment-portal", "wrong-key", false)]
    [InlineData("unknown-system", "secret-001", false)]
    [InlineData("", "secret-001", false)]
    [InlineData("assessment-portal", "", false)]
    public void IsValid_MatchesPerSystemKeys(string sourceSystem, string key, bool expected)
    {
        Assert.Equal(expected, ApiKeyValidator.IsValid(BuildConfig(), sourceSystem, key));
    }
}

public class SendGridPayloadBuilderTests
{
    [Fact]
    public void BuildPayload_ProducesDynamicTemplateSendShape()
    {
        var outgoing = new OutgoingEmail(
            "d-123",
            new Branding("no-reply@apc.example.org", "APC"),
            [new Recipient("jane@example.org", "Jane")],
            "Reset your password",
            new Dictionary<string, object?> { ["firstName"] = "Jane" },
            new Dictionary<string, string> { ["source_system"] = "assessment-portal" });

        var json = JsonSerializer.Serialize(SendGridPayloadBuilder.BuildPayload(outgoing));

        Assert.Contains("\"template_id\":\"d-123\"", json);
        Assert.Contains("\"dynamic_template_data\":", json);
        Assert.Contains("\"firstName\":\"Jane\"", json);
        Assert.Contains("jane@example.org", json);
        Assert.Contains("\"subject\":\"Reset your password\"", json);
    }
}
