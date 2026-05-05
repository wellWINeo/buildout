using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion.Mentions;
using Xunit;

namespace Buildout.UnitTests.Markdown.Mentions;

public class PageMentionConverterTests
{
    private readonly PageMentionConverter _sut = new();

    [Fact]
    public void WritesExpectedFormForCanonicalMention()
    {
        var mention = new PageMention { PageId = "pg-123" };
        var result = _sut.Render(mention, "My Page");
        Assert.Equal("[My Page](buildin://pg-123)", result);
    }

    [Fact]
    public void FallsBackToDisplayTextWhenSubFieldsMissing()
    {
        var mention = new PageMention { PageId = "" };
        var result = _sut.Render(mention, "Fallback Text");
        Assert.Equal("[Fallback Text](buildin://)", result);
    }
}
