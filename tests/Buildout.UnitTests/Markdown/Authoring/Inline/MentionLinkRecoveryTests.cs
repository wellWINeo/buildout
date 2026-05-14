using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Inline;
using Xunit;

namespace Buildout.UnitTests.Markdown.Authoring.Inline;

public class MentionLinkRecoveryTests
{
    [Fact]
    public void BuildinLink_ConvertedToPageMention()
    {
        var input = new List<RichText>
        {
            new() { Type = "text", Content = "My Page", Href = "buildin://abc123" }
        };
        var result = MentionLinkRecovery.Recover(input);
        Assert.Single(result);
        Assert.Equal("mention", result[0].Type);
        Assert.Null(result[0].Href);
        Assert.IsType<PageMention>(result[0].Mention);
        Assert.Equal("abc123", ((PageMention)result[0].Mention!).PageId);
    }

    [Fact]
    public void HttpLink_Unchanged()
    {
        var input = new List<RichText>
        {
            new() { Type = "text", Content = "click", Href = "https://example.com" }
        };
        var result = MentionLinkRecovery.Recover(input);
        Assert.Single(result);
        Assert.Equal("text", result[0].Type);
        Assert.Equal("https://example.com", result[0].Href);
    }

    [Fact]
    public void MixedLinks_ConvertsOnlyBuildinLinks()
    {
        var input = new List<RichText>
        {
            new() { Type = "text", Content = "Page", Href = "buildin://id1" },
            new() { Type = "text", Content = "link", Href = "https://example.com" }
        };
        var result = MentionLinkRecovery.Recover(input);
        Assert.Equal(2, result.Count);
        Assert.Equal("mention", result[0].Type);
        Assert.Equal("text", result[1].Type);
    }
}
