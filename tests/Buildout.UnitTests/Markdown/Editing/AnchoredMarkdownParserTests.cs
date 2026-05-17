using Buildout.Core.Markdown.Editing.Internal;
using Xunit;

namespace Buildout.UnitTests.Markdown.Editing;

public class AnchoredMarkdownParserTests
{
    private const string RootComment = "<!-- buildin:root -->";
    private const string BlockAbc = "<!-- buildin:block:abc123 -->";
    private const string BlockDef = "<!-- buildin:block:def456 -->";
    private const string BlockGhi = "<!-- buildin:block:ghi789 -->";
    private const string OpaqueUnsupported = "<!-- buildin:opaque:unsupported1 -->";

    [Fact]
    public void RootSentinel_ProducesRootAnchor()
    {
        var markdown = $"""
            {RootComment}
            # Title
            """;

        var result = AnchoredMarkdownParser.Parse(markdown);

        Assert.NotEmpty(result);
        var root = result[0];
        Assert.Equal(AnchorKind.Root, root.AnchorKind);
        Assert.NotNull(root.Children);
    }

    [Fact]
    public void BlockAnchor_ProducesBlockAnchorWithCorrectId()
    {
        var markdown = $"""
            {BlockAbc}
            First paragraph.
            """;

        var result = AnchoredMarkdownParser.Parse(markdown);

        Assert.NotEmpty(result);
        var block = result.First(b => b.AnchorKind == AnchorKind.Block);
        Assert.Equal("abc123", block.AnchorId);
        Assert.NotNull(block.Block);
    }

    [Fact]
    public void OpaqueAnchor_ProducesOpaqueAnchorWithCorrectId()
    {
        var markdown = $"""
            {OpaqueUnsupported}
            [child_page block]
            """;

        var result = AnchoredMarkdownParser.Parse(markdown);

        Assert.NotEmpty(result);
        var opaque = result.First(b => b.AnchorKind == AnchorKind.Opaque);
        Assert.Equal("unsupported1", opaque.AnchorId);
    }

    [Fact]
    public void NestedBlocks_HeadingWithChildren()
    {
        var markdown = $"""
            {RootComment}
            # Title

            {BlockAbc}
            First paragraph.

            {BlockDef}
            ## Section

            {OpaqueUnsupported}
            [child_page block]
            """;

        var result = AnchoredMarkdownParser.Parse(markdown);

        Assert.NotEmpty(result);
        var root = result[0];
        Assert.Equal(AnchorKind.Root, root.AnchorKind);
        Assert.NotEmpty(root.Children);
    }

    [Fact]
    public void MultipleBlocks_ProduceSiblingNodes()
    {
        var markdown = $"""
            {RootComment}
            # Title

            {BlockAbc}
            First paragraph.

            {BlockDef}
            Second paragraph.

            {BlockGhi}
            Third paragraph.
            """;

        var result = AnchoredMarkdownParser.Parse(markdown);

        Assert.NotEmpty(result);
        var root = result[0];
        Assert.Equal(3, root.Children.Count);
        Assert.Equal("abc123", root.Children[0].AnchorId);
        Assert.Equal("def456", root.Children[1].AnchorId);
        Assert.Equal("ghi789", root.Children[2].AnchorId);
    }

    [Fact]
    public void NoAnchors_BlocksGetNullAnchorId()
    {
        var markdown = """
            # Title

            First paragraph.

            Second paragraph.
            """;

        var result = AnchoredMarkdownParser.Parse(markdown);

        Assert.NotEmpty(result);
        Assert.All(result, node =>
        {
            if (node.AnchorKind != AnchorKind.Root)
            {
                Assert.Null(node.AnchorId);
            }
        });
    }

    [Fact]
    public void RoundTrip_NestedListItems()
    {
        var markdown = $"""
            {RootComment}
            # Title

            {BlockAbc}
            - item one
              - nested child
            - item two
            """;

        var result = AnchoredMarkdownParser.Parse(markdown);

        Assert.NotEmpty(result);
        var root = result[0];
        Assert.Equal(AnchorKind.Root, root.AnchorKind);
        Assert.NotEmpty(root.Children);
        var listBlock = root.Children[0];
        Assert.Equal("abc123", listBlock.AnchorId);
        Assert.Equal(AnchorKind.Block, listBlock.AnchorKind);
        Assert.NotNull(listBlock.Block);
    }
}
