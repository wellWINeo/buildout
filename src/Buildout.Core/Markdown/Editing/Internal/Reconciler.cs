using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring;

namespace Buildout.Core.Markdown.Editing.Internal;

public abstract record WriteOp
{
    public sealed record Update(string AnchorId, BlockSubtreeWrite Block) : WriteOp;
    public sealed record Delete(string AnchorId) : WriteOp;
    public sealed record Append(string ParentId, BlockSubtreeWrite Block) : WriteOp;
}

public static class Reconciler
{
    public static IReadOnlyList<WriteOp> Reconcile(
        IReadOnlyList<BlockSubtreeWithAnchor> originalTree,
        IReadOnlyList<BlockSubtreeWithAnchor> patchedTree)
    {
        var ops = new List<WriteOp>();
        var originalMap = new Dictionary<string, (BlockSubtreeWithAnchor Node, string? ParentAnchorId, int SiblingIndex)>();
        BuildOriginalMap(originalTree, parentAnchorId: null, originalMap);

        var seenAnchors = new HashSet<string>();
        WalkPatched(patchedTree, parentAnchorId: null, originalMap, seenAnchors, ops);

        foreach (var (anchorId, (node, _, _)) in originalMap)
        {
            if (seenAnchors.Contains(anchorId))
                continue;

            if (node.AnchorKind == AnchorKind.Opaque)
                throw new UnsupportedBlockTouchedException(anchorId);

            ops.Add(new WriteOp.Delete(anchorId));
        }

        return ops;
    }

    private static void BuildOriginalMap(
        IReadOnlyList<BlockSubtreeWithAnchor> nodes,
        string? parentAnchorId,
        Dictionary<string, (BlockSubtreeWithAnchor Node, string? ParentAnchorId, int SiblingIndex)> map)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node.AnchorId is not null)
            {
                map[node.AnchorId] = (node, parentAnchorId, i);
                BuildOriginalMap(node.Children, node.AnchorId, map);
            }
        }
    }

    private static void WalkPatched(
        IReadOnlyList<BlockSubtreeWithAnchor> nodes,
        string? parentAnchorId,
        Dictionary<string, (BlockSubtreeWithAnchor Node, string? ParentAnchorId, int SiblingIndex)> originalMap,
        HashSet<string> seenAnchors,
        List<WriteOp> ops)
    {
        var lastOriginalIndex = -1;

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            if (node.AnchorId is null)
            {
                if (node.Block is not null && parentAnchorId is not null)
                    ops.Add(new WriteOp.Append(parentAnchorId, node.Block));
                WalkPatched(node.Children, parentAnchorId, originalMap, seenAnchors, ops);
                continue;
            }

            seenAnchors.Add(node.AnchorId);

            if (originalMap.TryGetValue(node.AnchorId, out var original))
            {
                if (original.ParentAnchorId != parentAnchorId)
                    throw new ReorderNotSupportedException(node.AnchorId, original.SiblingIndex, i);

                if (original.SiblingIndex < lastOriginalIndex)
                    throw new ReorderNotSupportedException(node.AnchorId, original.SiblingIndex, i);

                lastOriginalIndex = original.SiblingIndex;

                if (!ContentEquals(original.Node.Block, node.Block))
                    ops.Add(new WriteOp.Update(node.AnchorId, node.Block!));
            }

            WalkPatched(node.Children, node.AnchorId, originalMap, seenAnchors, ops);
        }
    }

    private static bool ContentEquals(BlockSubtreeWrite? a, BlockSubtreeWrite? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (!BlockEquals(a.Block, b.Block)) return false;
        if (a.Children.Count != b.Children.Count) return false;
        for (var i = 0; i < a.Children.Count; i++)
            if (!ContentEquals(a.Children[i], b.Children[i])) return false;
        return true;
    }

    private static bool BlockEquals(Block? a, Block? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.GetType() != b.GetType()) return false;
        if (a.Id != b.Id || a.HasChildren != b.HasChildren ||
            a.CreatedAt != b.CreatedAt || a.LastEditedAt != b.LastEditedAt ||
            a.Parent != b.Parent) return false;

        return a switch
        {
            ParagraphBlock => SeqEq(((ParagraphBlock)a).RichTextContent, ((ParagraphBlock)b).RichTextContent),
            Heading1Block => SeqEq(((Heading1Block)a).RichTextContent, ((Heading1Block)b).RichTextContent),
            Heading2Block => SeqEq(((Heading2Block)a).RichTextContent, ((Heading2Block)b).RichTextContent),
            Heading3Block => SeqEq(((Heading3Block)a).RichTextContent, ((Heading3Block)b).RichTextContent),
            BulletedListItemBlock => SeqEq(((BulletedListItemBlock)a).RichTextContent, ((BulletedListItemBlock)b).RichTextContent),
            NumberedListItemBlock => SeqEq(((NumberedListItemBlock)a).RichTextContent, ((NumberedListItemBlock)b).RichTextContent),
            ToDoBlock td => SeqEq(td.RichTextContent, ((ToDoBlock)b).RichTextContent) && td.Checked == ((ToDoBlock)b).Checked,
            ToggleBlock => SeqEq(((ToggleBlock)a).RichTextContent, ((ToggleBlock)b).RichTextContent),
            CodeBlock cb => SeqEq(cb.RichTextContent, ((CodeBlock)b).RichTextContent) && cb.Language == ((CodeBlock)b).Language,
            QuoteBlock => SeqEq(((QuoteBlock)a).RichTextContent, ((QuoteBlock)b).RichTextContent),
            ImageBlock ib => ib.Url == ((ImageBlock)b).Url && SeqEq(ib.Caption, ((ImageBlock)b).Caption),
            EmbedBlock eb => eb.Url == ((EmbedBlock)b).Url,
            TableBlock tb => tb.TableWidth == ((TableBlock)b).TableWidth && tb.HasColumnHeader == ((TableBlock)b).HasColumnHeader && tb.HasRowHeader == ((TableBlock)b).HasRowHeader,
            TableRowBlock => CellsEq(((TableRowBlock)a).Cells, ((TableRowBlock)b).Cells),
            ChildPageBlock cp => cp.Title == ((ChildPageBlock)b).Title,
            ChildDatabaseBlock cd => cd.Title == ((ChildDatabaseBlock)b).Title,
            SyncedBlock sb => sb.SyncedFromId == ((SyncedBlock)b).SyncedFromId,
            LinkPreviewBlock lp => lp.Url == ((LinkPreviewBlock)b).Url,
            _ => true,
        };
    }

    private static bool SeqEq<T>(IReadOnlyList<T>? a, IReadOnlyList<T>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (!Equals(a[i], b[i])) return false;
        return true;
    }

    private static bool CellsEq(IReadOnlyList<IReadOnlyList<RichText>>? a, IReadOnlyList<IReadOnlyList<RichText>>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (!SeqEq(a[i], b[i])) return false;
        return true;
    }
}
