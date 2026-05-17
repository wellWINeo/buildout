using Buildout.Core.Markdown.Editing.Internal;
using Xunit;

namespace Buildout.UnitTests.Markdown.Editing;

public class RevisionTokenComputerTests
{
    private static readonly string HexPattern = "^[0-9a-f]{8}$";

    [Fact]
    public void Compute_ReturnsEightLowercaseHexChars()
    {
        var token = RevisionTokenComputer.Compute("# Hello World\n\nSome content.");
        Assert.Matches(HexPattern, token);
    }

    [Fact]
    public void Compute_SameInput_ReturnsSameToken()
    {
        const string markdown = "# Identical Input\n\nParagraph.";
        var first = RevisionTokenComputer.Compute(markdown);
        var second = RevisionTokenComputer.Compute(markdown);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_DifferentInput_ReturnsDifferentToken()
    {
        var tokenA = RevisionTokenComputer.Compute("markdown A");
        var tokenB = RevisionTokenComputer.Compute("markdown B");
        Assert.NotEqual(tokenA, tokenB);
    }

    [Fact]
    public void Compute_IsPureFunction_IdenticalStringsSameToken()
    {
        const string markdown = "# A page\n\nWith some text.";
        Assert.Equal(
            RevisionTokenComputer.Compute(markdown),
            RevisionTokenComputer.Compute(markdown));
    }

    [Fact]
    public void Compute_SingleCharChange_ProducesDifferentToken()
    {
        var original = RevisionTokenComputer.Compute("# Hello World");
        var modified = RevisionTokenComputer.Compute("# Hello Worle");
        Assert.NotEqual(original, modified);
    }

    [Fact]
    public void Compute_EmptyInput_ReturnsValidToken()
    {
        var token = RevisionTokenComputer.Compute("");
        Assert.Matches(HexPattern, token);
    }

    [Fact]
    public void Compute_LongMultiParagraph_ReturnsValidToken()
    {
        var markdown = string.Join("\n\n", """
            # Long Document

            First paragraph with some content that spans a reasonable length.

            ## Section One

            Another paragraph here with different text.

            ## Section Two

            Yet more content in this section.

            - List item one
            - List item two
            - List item three
            """.Split("\n\n"));
        var token = RevisionTokenComputer.Compute(markdown);
        Assert.Matches(HexPattern, token);
    }

    [Fact]
    public void Compute_UnicodeInput_ReturnsValidToken()
    {
        var markdown = "# Überschrift 🎉\n\nこんにちは世界\n\nÉléphant café";
        var token = RevisionTokenComputer.Compute(markdown);
        Assert.Matches(HexPattern, token);
    }
}
