using EmailCentral.Api.Templates;
using Xunit;

namespace EmailCentral.Tests;

public class PreviewRendererTests
{
    [Fact]
    public void Render_SubstitutesTokensCaseInsensitively()
    {
        const string html = "<p>Hello {{firstName}}, link: {{resetLink}}</p>";
        var data = new Dictionary<string, object?> { ["FirstName"] = "Jane", ["resetLink"] = "https://x/r?t=1" };

        var result = PreviewRenderer.Render(html, data);

        Assert.Equal("<p>Hello Jane, link: https://x/r?t=1</p>", result);
    }

    [Fact]
    public void Render_LeavesUnknownTokensIntact()
    {
        var result = PreviewRenderer.Render("<p>{{unknown}}</p>", new Dictionary<string, object?>());

        Assert.Equal("<p>{{unknown}}</p>", result);
    }

    [Fact]
    public void Render_HandlesNullDataSafely()
    {
        Assert.Equal("<p>x</p>", PreviewRenderer.Render("<p>x</p>", null));
    }
}
