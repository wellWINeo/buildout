using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Markdown.Editing;
using Buildout.Core.Markdown.Editing.Internal;
using Buildout.Core.Markdown.Editing.PatchOperations;
using Xunit;

namespace Buildout.UnitTests.Markdown.Editing;

public class PatchApplicatorTests
{
    private readonly PatchApplicator _sut = new();

    private static BlockSubtreeWithAnchor Root(params BlockSubtreeWithAnchor[] children) =>
        new() { AnchorKind = AnchorKind.Root, Children = children };

    private static BlockSubtreeWithAnchor Block(string anchorId, BlockSubtreeWrite? block = null, params BlockSubtreeWithAnchor[] children) =>
        new() { AnchorId = anchorId, AnchorKind = AnchorKind.Block, Block = block ?? MakeParagraph(), Children = children };

    private static BlockSubtreeWithAnchor Opaque(string anchorId, BlockSubtreeWrite? block = null) =>
        new() { AnchorId = anchorId, AnchorKind = AnchorKind.Opaque, Block = block ?? MakeParagraph(), Children = [] };

    private static BlockSubtreeWithAnchor Unanchored(BlockSubtreeWrite? block = null) =>
        new() { AnchorId = null, AnchorKind = AnchorKind.Block, Block = block ?? MakeParagraph(), Children = [] };

    private static BlockSubtreeWrite MakeParagraph(string? text = null) =>
        new()
        {
            Block = text is null
                ? new ParagraphBlock()
                : new ParagraphBlock
                {
                    RichTextContent = [new RichText { Type = "text", Content = text }]
                },
            Children = []
        };

    private static BlockSubtreeWrite MakeHeading(string? text = null) =>
        new()
        {
            Block = text is null
                ? new Heading1Block()
                : new Heading1Block
                {
                    RichTextContent = [new RichText { Type = "text", Content = text }]
                },
            Children = []
        };

    private static BlockSubtreeWithAnchor[] SingleRoot(params BlockSubtreeWithAnchor[] children) =>
        [Root(children)];

    [Fact]
    public void ReplaceBlock_HappyPath_ReplacesAnchorSubtree()
    {
        var tree = SingleRoot(
            Block("a", MakeParagraph()),
            Block("b", MakeParagraph()));

        var op = new ReplaceBlockOperation { Anchor = "a", Markdown = "new content" };

        var result = _sut.Apply(tree, [op]);

        Assert.Single(result);
        var root = result[0];
        Assert.Equal(2, root.Children.Count);
    }

    [Fact]
    public void ReplaceBlock_UnknownAnchor_RaisesUnknownAnchorException()
    {
        var tree = SingleRoot(Block("a"));

        var op = new ReplaceBlockOperation { Anchor = "nonexistent", Markdown = "text" };

        Assert.Throws<UnknownAnchorException>(() => _sut.Apply(tree, [op]));
    }

    [Fact]
    public void ReplaceBlock_OpaqueAnchor_RaisesUnsupportedBlockTouchedException()
    {
        var tree = SingleRoot(Opaque("opaque1"));

        var op = new ReplaceBlockOperation { Anchor = "opaque1", Markdown = "text" };

        Assert.Throws<UnsupportedBlockTouchedException>(() => _sut.Apply(tree, [op]));
    }

    [Fact]
    public void ReplaceSection_HappyPath_ReplacesHeadingAndSiblings()
    {
        var tree = SingleRoot(
            Block("heading1", MakeHeading()),
            Block("para1"),
            Block("heading2", MakeHeading()),
            Block("para2"));

        var op = new ReplaceSectionOperation { Anchor = "heading1", Markdown = "## Replaced\n\nNew text." };

        var result = _sut.Apply(tree, [op]);

        Assert.Single(result);
        var root = result[0];
        Assert.NotNull(root);
    }

    [Fact]
    public void ReplaceSection_UnknownAnchor_RaisesUnknownAnchorException()
    {
        var tree = SingleRoot(Block("a"));

        var op = new ReplaceSectionOperation { Anchor = "nonexistent", Markdown = "text" };

        Assert.Throws<UnknownAnchorException>(() => _sut.Apply(tree, [op]));
    }

    [Fact]
    public void ReplaceSection_AnchorNotHeading_RaisesSectionAnchorNotHeadingException()
    {
        var tree = SingleRoot(Block("not-heading", MakeParagraph()));

        var op = new ReplaceSectionOperation { Anchor = "not-heading", Markdown = "text" };

        Assert.Throws<SectionAnchorNotHeadingException>(() => _sut.Apply(tree, [op]));
    }

    [Fact]
    public void ReplaceSection_OpaqueInScope_RaisesUnsupportedBlockTouchedException()
    {
        var tree = SingleRoot(
            Block("h1", MakeHeading()),
            Opaque("opaque1"));

        var op = new ReplaceSectionOperation { Anchor = "h1", Markdown = "text" };

        Assert.Throws<UnsupportedBlockTouchedException>(() => _sut.Apply(tree, [op]));
    }

    [Fact]
    public void SearchReplace_HappyPath_ReplacesFirstOccurrence()
    {
        var tree = SingleRoot(
            Block("a", MakeParagraph("old content")),
            Block("b", MakeParagraph()));

        var op = new SearchReplaceOperation { OldStr = "old", NewStr = "new" };

        var result = _sut.Apply(tree, [op]);

        Assert.Single(result);
        Assert.NotNull(result[0]);
    }

    [Fact]
    public void SearchReplace_NoMatch_RaisesNoMatchException()
    {
        var tree = SingleRoot(Block("a"));

        var op = new SearchReplaceOperation { OldStr = "does not exist", NewStr = "replacement" };

        Assert.Throws<NoMatchException>(() => _sut.Apply(tree, [op]));
    }

    [Fact]
    public void SearchReplace_AmbiguousMatch_RaisesAmbiguousMatchException()
    {
        var tree = SingleRoot(
            Block("a", MakeParagraph("duplicate text")),
            Block("b", MakeParagraph("duplicate text")));

        var op = new SearchReplaceOperation { OldStr = "duplicate", NewStr = "replacement" };

        Assert.Throws<AmbiguousMatchException>(() => _sut.Apply(tree, [op]));
    }

    [Fact]
    public void AppendSection_HappyPath_AppendsToContainer()
    {
        var tree = SingleRoot(
            Block("container", MakeParagraph(), Block("child")));

        var op = new AppendSectionOperation { Anchor = "container", Markdown = "Appended paragraph." };

        var result = _sut.Apply(tree, [op]);

        Assert.Single(result);
        var container = result[0].Children[0];
        Assert.Equal(2, container.Children.Count);
    }

    [Fact]
    public void AppendSection_NullAnchor_AppendsToRoot()
    {
        var tree = SingleRoot(Block("a"));

        var op = new AppendSectionOperation { Anchor = null, Markdown = "Appended." };

        var result = _sut.Apply(tree, [op]);

        Assert.Single(result);
        var root = result[0];
        Assert.Equal(2, root.Children.Count);
    }

    [Fact]
    public void AppendSection_UnknownAnchor_RaisesUnknownAnchorException()
    {
        var tree = SingleRoot(Block("a"));

        var op = new AppendSectionOperation { Anchor = "nonexistent", Markdown = "text" };

        Assert.Throws<UnknownAnchorException>(() => _sut.Apply(tree, [op]));
    }

    [Fact]
    public void AppendSection_LeafParagraphBlock_RaisesAnchorNotContainerException()
    {
        var tree = SingleRoot(
            Block("leaf-para", MakeParagraph()));

        var op = new AppendSectionOperation { Anchor = "leaf-para", Markdown = "text" };

        Assert.Throws<AnchorNotContainerException>(() => _sut.Apply(tree, [op]));
    }

    [Fact]
    public void InsertAfterBlock_HappyPath_InsertsAfterAnchor()
    {
        var tree = SingleRoot(
            Block("a"),
            Block("b"));

        var op = new InsertAfterBlockOperation { Anchor = "a", Markdown = "Inserted." };

        var result = _sut.Apply(tree, [op]);

        Assert.Single(result);
        var root = result[0];
        Assert.Equal(3, root.Children.Count);
    }

    [Fact]
    public void InsertAfterBlock_UnknownAnchor_RaisesUnknownAnchorException()
    {
        var tree = SingleRoot(Block("a"));

        var op = new InsertAfterBlockOperation { Anchor = "nonexistent", Markdown = "text" };

        Assert.Throws<UnknownAnchorException>(() => _sut.Apply(tree, [op]));
    }

    [Fact]
    public void InsertAfterBlock_OpaqueAnchor_RaisesUnsupportedBlockTouchedException()
    {
        var tree = SingleRoot(Opaque("opaque1"));

        var op = new InsertAfterBlockOperation { Anchor = "opaque1", Markdown = "text" };

        Assert.Throws<UnsupportedBlockTouchedException>(() => _sut.Apply(tree, [op]));
    }

    [Fact]
    public void InsertAfterBlock_AnchorIsRoot_RaisesUnknownAnchorException()
    {
        var tree = SingleRoot(Block("a"));

        var op = new InsertAfterBlockOperation { Anchor = "root", Markdown = "text" };

        Assert.Throws<UnknownAnchorException>(() => _sut.Apply(tree, [op]));
    }

    [Fact]
    public void SequentialOperations_Op2SeesTreeFromOp1()
    {
        var tree = SingleRoot(
            Block("a", MakeParagraph()),
            Block("b", MakeParagraph()));

        var op1 = new ReplaceBlockOperation { Anchor = "a", Markdown = "first replacement" };
        var op2 = new SearchReplaceOperation { OldStr = "first replacement", NewStr = "second replacement" };

        var result = _sut.Apply(tree, [op1, op2]);

        Assert.Single(result);
        Assert.NotNull(result[0]);
    }
}
