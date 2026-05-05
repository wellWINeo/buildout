using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion.Mentions;
using Xunit;

namespace Buildout.UnitTests.Markdown.Mentions;

public class UserMentionConverterTests
{
    private readonly UserMentionConverter _sut = new();

    [Fact]
    public void WritesExpectedFormForCanonicalMention()
    {
        var mention = new UserMention { UserId = "u-1", DisplayName = "Alice" };
        var result = _sut.Render(mention, "ignored");
        Assert.Equal("@Alice", result);
    }

    [Fact]
    public void FallsBackToDisplayTextWhenSubFieldsMissing()
    {
        var mention = new UserMention { UserId = "u-2", DisplayName = null };
        var result = _sut.Render(mention, "Bob");
        Assert.Equal("@Bob", result);
    }
}
