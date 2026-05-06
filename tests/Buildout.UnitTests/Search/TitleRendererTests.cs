using Buildout.Core.Buildin.Models;
using Buildout.Core.Search.Internal;
using Xunit;

namespace Buildout.UnitTests.Search;

public sealed class TitleRendererTests
{
    private readonly TitleRenderer _renderer = new();

    [Fact]
    public void NullInput_ReturnsUntitled()
    {
        var result = _renderer.RenderPlain(null);

        Assert.Equal("(untitled)", result);
    }

    [Fact]
    public void EmptyList_ReturnsUntitled()
    {
        var result = _renderer.RenderPlain([]);

        Assert.Equal("(untitled)", result);
    }

    [Fact]
    public void SingleTextSegment_ReturnsContent()
    {
        var title = new List<RichText>
        {
            new() { Type = "text", Content = "Hello" }
        };

        var result = _renderer.RenderPlain(title);

        Assert.Equal("Hello", result);
    }

    [Fact]
    public void MultipleSegments_ConcatenatesInOrder()
    {
        var title = new List<RichText>
        {
            new() { Type = "text", Content = "Hello " },
            new() { Type = "text", Content = "World" }
        };

        var result = _renderer.RenderPlain(title);

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void MentionSegment_UsesContent()
    {
        var title = new List<RichText>
        {
            new() { Type = "mention", Content = "@user" }
        };

        var result = _renderer.RenderPlain(title);

        Assert.Equal("@user", result);
    }

    [Fact]
    public void TabInContent_ReplacedWithSpace()
    {
        var title = new List<RichText>
        {
            new() { Type = "text", Content = "Hello\tWorld" }
        };

        var result = _renderer.RenderPlain(title);

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void WhitespaceOnlyResult_ReturnsUntitled()
    {
        var title = new List<RichText>
        {
            new() { Type = "text", Content = "   " }
        };

        var result = _renderer.RenderPlain(title);

        Assert.Equal("(untitled)", result);
    }

    [Fact]
    public void AllEmptyContents_ReturnsUntitled()
    {
        var title = new List<RichText>
        {
            new() { Type = "text", Content = "" },
            new() { Type = "text", Content = "" }
        };

        var result = _renderer.RenderPlain(title);

        Assert.Equal("(untitled)", result);
    }
}
