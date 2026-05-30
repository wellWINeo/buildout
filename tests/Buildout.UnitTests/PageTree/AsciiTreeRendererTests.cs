using Buildout.Core.PageTree;
using Buildout.Core.PageTree.Rendering;
using Xunit;

namespace Buildout.UnitTests.PageTree;

public sealed class AsciiTreeRendererTests
{
    private static AsciiTreeRenderer CreateSut() => new AsciiTreeRenderer();

    private static TreeNode Leaf(string name, string uri = "https://x.com") =>
        new(name, uri, Array.Empty<TreeNode>());

    [Fact]
    public void SingleNode_NoConnectors()
    {
        var sut = CreateSut();
        var root = Leaf("Root", "https://example.com/root");

        var result = sut.Render(root);

        Assert.Equal("[Root](<https://example.com/root>)", result);
    }

    [Fact]
    public void SingleChild_UsesLastChildGlyph()
    {
        var sut = CreateSut();
        var root = new TreeNode("Root", "https://r.com", [Leaf("Only Child", "https://c.com")]);

        var result = sut.Render(root);
        var lines = result.Split('\n');

        Assert.Equal("[Root](<https://r.com>)", lines[0]);
        Assert.Equal("└── [Only Child](<https://c.com>)", lines[1]);
    }

    [Fact]
    public void MultipleChildren_LastChildUsesCornerGlyph()
    {
        var sut = CreateSut();
        var root = new TreeNode("Root", "https://r.com", [
            Leaf("First", "https://first.com"),
            Leaf("Second", "https://second.com"),
            Leaf("Third", "https://third.com"),
        ]);

        var result = sut.Render(root);
        var lines = result.Split('\n');

        Assert.Equal("├── [First](<https://first.com>)", lines[1]);
        Assert.Equal("├── [Second](<https://second.com>)", lines[2]);
        Assert.Equal("└── [Third](<https://third.com>)", lines[3]);
    }

    [Fact]
    public void IntermediateChild_UsesTeeGlyph()
    {
        var sut = CreateSut();
        var root = new TreeNode("Root", "https://r.com", [
            Leaf("A"),
            Leaf("B"),
        ]);

        var result = sut.Render(root);
        var lines = result.Split('\n');

        Assert.StartsWith("├── ", lines[1]);
        Assert.StartsWith("└── ", lines[2]);
    }

    [Fact]
    public void NestedChildren_UsesVerticalBarContinuation()
    {
        var sut = CreateSut();
        var root = new TreeNode("Root", "https://r.com", [
            new TreeNode("A", "https://a.com", [Leaf("A1", "https://a1.com")]),
            Leaf("B", "https://b.com"),
        ]);

        var result = sut.Render(root);
        var lines = result.Split('\n');

        Assert.Equal("├── [A](<https://a.com>)", lines[1]);
        Assert.Equal("│   └── [A1](<https://a1.com>)", lines[2]);
        Assert.Equal("└── [B](<https://b.com>)", lines[3]);
    }

    [Fact]
    public void NestedUnderLastChild_UsesFourSpaceGutter()
    {
        var sut = CreateSut();
        var root = new TreeNode("Root", "https://r.com", [
            new TreeNode("Last", "https://last.com", [Leaf("Child", "https://child.com")]),
        ]);

        var result = sut.Render(root);
        var lines = result.Split('\n');

        Assert.Equal("└── [Last](<https://last.com>)", lines[1]);
        Assert.Equal("    └── [Child](<https://child.com>)", lines[2]);
    }

    [Fact]
    public void DeepNesting_ProducesCorrectGlyphColumns()
    {
        var sut = CreateSut();
        var root = new TreeNode("Root", "https://r.com", [
            new TreeNode("A", "https://a.com", [
                new TreeNode("A1", "https://a1.com", [Leaf("A1a", "https://a1a.com")])
            ]),
            Leaf("B", "https://b.com"),
        ]);

        var result = sut.Render(root);
        var lines = result.Split('\n');

        Assert.Equal("[Root](<https://r.com>)", lines[0]);
        Assert.Equal("├── [A](<https://a.com>)", lines[1]);
        Assert.Equal("│   └── [A1](<https://a1.com>)", lines[2]);
        Assert.Equal("│       └── [A1a](<https://a1a.com>)", lines[3]);
        Assert.Equal("└── [B](<https://b.com>)", lines[4]);
    }

    [Fact]
    public void NameWithClosingBracket_IsEscaped()
    {
        var sut = CreateSut();
        var root = Leaf("Name]With]Brackets");

        var result = sut.Render(root);

        Assert.Contains("Name\\]With\\]Brackets", result);
    }

    [Fact]
    public void NameWithOpeningBracket_IsEscaped()
    {
        var sut = CreateSut();
        var root = Leaf("Name[With[Brackets");

        var result = sut.Render(root);

        Assert.Contains("Name\\[With\\[Brackets", result);
    }

    [Fact]
    public void NameWithLessThan_IsEscaped()
    {
        var sut = CreateSut();
        var root = Leaf("Name<With<Angles");

        var result = sut.Render(root);

        Assert.Contains("Name\\<With\\<Angles", result);
    }

    [Fact]
    public void NameWithGreaterThan_IsEscaped()
    {
        var sut = CreateSut();
        var root = Leaf("Name>With>Angles");

        var result = sut.Render(root);

        Assert.Contains("Name\\>With\\>Angles", result);
    }

    [Fact]
    public void NameWithBackslash_IsEscaped()
    {
        var sut = CreateSut();
        var root = Leaf("Name\\With\\Backslash");

        var result = sut.Render(root);

        Assert.Contains("Name\\\\With\\\\Backslash", result);
    }

    [Fact]
    public void UriUsesAngleBracketForm()
    {
        var sut = CreateSut();
        var root = Leaf("Node", "https://example.com/path?q=1&a=2");

        var result = sut.Render(root);

        Assert.Contains("(<https://example.com/path?q=1&a=2>)", result);
    }

    [Fact]
    public void UntitledNode_RendersWithUntitledPlaceholder()
    {
        var sut = CreateSut();
        var root = Leaf("(untitled)", "https://x.com");

        var result = sut.Render(root);

        Assert.Contains("(untitled)", result);
    }

    [Fact]
    public void UnavailableNode_RendersWithUnavailablePlaceholder()
    {
        var sut = CreateSut();
        var root = new TreeNode("Root", "https://r.com", [
            new TreeNode("(unavailable)", "", Array.Empty<TreeNode>())
        ]);

        var result = sut.Render(root);

        Assert.Contains("(unavailable)", result);
    }

    [Fact]
    public void NameWithNewline_NormalizedToSpace()
    {
        var sut = CreateSut();
        var root = Leaf("Name\nWith\nNewlines");

        var result = sut.Render(root);

        Assert.Contains("Name With Newlines", result);
        Assert.DoesNotContain('\n', result.Split('\n')[0]);
    }

    [Fact]
    public void NoTrailingNewlineAfterLastLine()
    {
        var sut = CreateSut();
        var root = new TreeNode("Root", "https://r.com", [Leaf("Child")]);

        var result = sut.Render(root);

        Assert.False(result.EndsWith('\n'));
    }

    [Fact]
    public void Format_IsAscii()
    {
        var sut = CreateSut();
        Assert.Equal(TreeFormat.Ascii, sut.Format);
    }
}
