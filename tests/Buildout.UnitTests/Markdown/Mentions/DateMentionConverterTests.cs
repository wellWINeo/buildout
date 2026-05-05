using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion.Mentions;
using Xunit;

namespace Buildout.UnitTests.Markdown.Mentions;

public class DateMentionConverterTests
{
    private readonly DateMentionConverter _sut = new();

    [Fact]
    public void WritesExpectedFormForCanonicalMention_StartOnly()
    {
        var mention = new DateMention { Start = "2025-01-15", End = null };
        var result = _sut.Render(mention, "ignored");
        Assert.Equal("2025-01-15", result);
    }

    [Fact]
    public void WritesExpectedFormForCanonicalMention_StartAndEnd()
    {
        var mention = new DateMention { Start = "2025-01-15", End = "2025-01-20" };
        var result = _sut.Render(mention, "ignored");
        Assert.Equal("2025-01-15 \u2013 2025-01-20", result);
    }

    [Fact]
    public void FallsBackToDisplayTextWhenSubFieldsMissing()
    {
        var mention = new DateMention { Start = "", End = null };
        var result = _sut.Render(mention, "Fallback Text");
        Assert.Equal("Fallback Text", result);
    }
}
