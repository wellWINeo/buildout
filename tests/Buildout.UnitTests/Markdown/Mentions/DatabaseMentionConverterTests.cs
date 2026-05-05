using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion.Mentions;
using Xunit;

namespace Buildout.UnitTests.Markdown.Mentions;

public class DatabaseMentionConverterTests
{
    private readonly DatabaseMentionConverter _sut = new();

    [Fact]
    public void WritesExpectedFormForCanonicalMention()
    {
        var mention = new DatabaseMention { DatabaseId = "db-456" };
        var result = _sut.Render(mention, "My Database");
        Assert.Equal("[My Database](buildin://db-456)", result);
    }

    [Fact]
    public void FallsBackToDisplayTextWhenSubFieldsMissing()
    {
        var mention = new DatabaseMention { DatabaseId = "" };
        var result = _sut.Render(mention, "Fallback Text");
        Assert.Equal("[Fallback Text](buildin://)", result);
    }
}
