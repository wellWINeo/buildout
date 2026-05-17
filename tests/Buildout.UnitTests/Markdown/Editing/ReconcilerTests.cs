using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Markdown.Editing;
using Buildout.Core.Markdown.Editing.Internal;
using Xunit;

namespace Buildout.UnitTests.Markdown.Editing;

public class ReconcilerTests
{
    private static BlockSubtreeWrite ParaWrite(string text) =>
        new()
        {
            Block = new ParagraphBlock
            {
                RichTextContent = [new RichText { Type = "text", Content = text }],
            },
            Children = [],
        };

    private static BlockSubtreeWithAnchor RootAnchor(
        string? anchorId = null,
        BlockSubtreeWrite? block = null,
        IReadOnlyList<BlockSubtreeWithAnchor>? children = null) =>
        new()
        {
            AnchorId = anchorId,
            AnchorKind = AnchorKind.Root,
            Block = block,
            Children = children ?? [],
        };

    private static BlockSubtreeWithAnchor BlockAnchor(
        string anchorId,
        BlockSubtreeWrite? block = null,
        IReadOnlyList<BlockSubtreeWithAnchor>? children = null) =>
        new()
        {
            AnchorId = anchorId,
            AnchorKind = AnchorKind.Block,
            Block = block,
            Children = children ?? [],
        };

    private static BlockSubtreeWithAnchor OpaqueAnchor(
        string anchorId,
        BlockSubtreeWrite? block = null) =>
        new()
        {
            AnchorId = anchorId,
            AnchorKind = AnchorKind.Opaque,
            Block = block,
            Children = [],
        };

    private static BlockSubtreeWithAnchor NewBlock(
        BlockSubtreeWrite? block = null,
        IReadOnlyList<BlockSubtreeWithAnchor>? children = null) =>
        new()
        {
            AnchorId = null,
            AnchorKind = AnchorKind.Block,
            Block = block,
            Children = children ?? [],
        };

    [Fact]
    public void IdenticalTrees_ProduceZeroWriteOps()
    {
        var tree = new[]
        {
            RootAnchor("root1", ParaWrite("hello"), [
                BlockAnchor("b1", ParaWrite("content")),
            ]),
        };

        var ops = Reconciler.Reconcile(tree, tree);

        Assert.Empty(ops);
    }

    [Fact]
    public void ChangedBlock_ProducesOneUpdateOp()
    {
        var original = new[]
        {
            RootAnchor("root1", ParaWrite("hello"), [
                BlockAnchor("b1", ParaWrite("old")),
            ]),
        };

        var patched = new[]
        {
            RootAnchor("root1", ParaWrite("hello"), [
                BlockAnchor("b1", ParaWrite("new")),
            ]),
        };

        var ops = Reconciler.Reconcile(original, patched);

        var update = Assert.Single(ops);
        var updateOp = Assert.IsType<WriteOp.Update>(update);
        Assert.Equal("b1", updateOp.AnchorId);
    }

    [Fact]
    public void DeletedBlock_ProducesOneDeleteOp()
    {
        var original = new[]
        {
            RootAnchor("root1", ParaWrite("hello"), [
                BlockAnchor("b1", ParaWrite("kept")),
                BlockAnchor("b2", ParaWrite("removed")),
            ]),
        };

        var patched = new[]
        {
            RootAnchor("root1", ParaWrite("hello"), [
                BlockAnchor("b1", ParaWrite("kept")),
            ]),
        };

        var ops = Reconciler.Reconcile(original, patched);

        var delete = Assert.Single(ops);
        var deleteOp = Assert.IsType<WriteOp.Delete>(delete);
        Assert.Equal("b2", deleteOp.AnchorId);
    }

    [Fact]
    public void NewBlock_ProducesAppendOpAgainstCorrectParent()
    {
        var original = new[]
        {
            RootAnchor("root1", ParaWrite("hello"), [
                BlockAnchor("b1", ParaWrite("existing")),
            ]),
        };

        var patched = new[]
        {
            RootAnchor("root1", ParaWrite("hello"), [
                BlockAnchor("b1", ParaWrite("existing")),
                NewBlock(ParaWrite("added")),
            ]),
        };

        var ops = Reconciler.Reconcile(original, patched);

        var append = Assert.Single(ops);
        var appendOp = Assert.IsType<WriteOp.Append>(append);
        Assert.Equal("root1", appendOp.ParentId);
    }

    [Fact]
    public void AnchorAtDifferentParent_ThrowsReorderNotSupportedException()
    {
        var original = new[]
        {
            RootAnchor("root1", ParaWrite("hello"), [
                BlockAnchor("b1", ParaWrite("content")),
            ]),
            RootAnchor("root2", ParaWrite("world"), []),
        };

        var patched = new[]
        {
            RootAnchor("root1", ParaWrite("hello"), []),
            RootAnchor("root2", ParaWrite("world"), [
                BlockAnchor("b1", ParaWrite("content")),
            ]),
        };

        Assert.Throws<ReorderNotSupportedException>(() =>
            Reconciler.Reconcile(original, patched));
    }

    [Fact]
    public void OpaqueAnchorRemoved_ThrowsUnsupportedBlockTouchedException()
    {
        var original = new[]
        {
            RootAnchor("root1", ParaWrite("hello"), [
                OpaqueAnchor("opaque1", ParaWrite("[child_page]")),
                BlockAnchor("b1", ParaWrite("content")),
            ]),
        };

        var patched = new[]
        {
            RootAnchor("root1", ParaWrite("hello"), [
                BlockAnchor("b1", ParaWrite("content")),
            ]),
        };

        Assert.Throws<UnsupportedBlockTouchedException>(() =>
            Reconciler.Reconcile(original, patched));
    }

    [Fact]
    public void MixedChanges_TotalOpsEqualsUpdatedPlusNewPlusDeleted()
    {
        var original = new[]
        {
            RootAnchor("root1", ParaWrite("hello"), [
                BlockAnchor("b1", ParaWrite("updated")),
                BlockAnchor("b2", ParaWrite("deleted")),
                BlockAnchor("b3", ParaWrite("untouched")),
            ]),
        };

        var patched = new[]
        {
            RootAnchor("root1", ParaWrite("hello"), [
                BlockAnchor("b1", ParaWrite("changed")),
                BlockAnchor("b3", ParaWrite("untouched")),
                NewBlock(ParaWrite("new-block")),
            ]),
        };

        var ops = Reconciler.Reconcile(original, patched);

        var updates = ops.Count(o => o is WriteOp.Update);
        var deletes = ops.Count(o => o is WriteOp.Delete);
        var appends = ops.Count(o => o is WriteOp.Append);

        Assert.Equal(1, updates);
        Assert.Equal(1, deletes);
        Assert.Equal(1, appends);
        Assert.Equal(updates + deletes + appends, ops.Count);
    }

    [Fact]
    public void UnchangedBlocksProduceNoOps_EvenWithOtherChanges()
    {
        var original = new[]
        {
            RootAnchor("root1", ParaWrite("hello"), [
                BlockAnchor("b1", ParaWrite("same")),
                BlockAnchor("b2", ParaWrite("will-change")),
            ]),
        };

        var patched = new[]
        {
            RootAnchor("root1", ParaWrite("hello"), [
                BlockAnchor("b1", ParaWrite("same")),
                BlockAnchor("b2", ParaWrite("different")),
            ]),
        };

        var ops = Reconciler.Reconcile(original, patched);

        Assert.DoesNotContain(ops, o => o is WriteOp.Update u && u.AnchorId == "b1");
        Assert.Contains(ops, o => o is WriteOp.Update u && u.AnchorId == "b2");
    }
}
