using System.Text;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Editing.PatchOperations;

namespace Buildout.Core.Markdown.Editing.Internal;

public sealed class PatchApplicator
{
#pragma warning disable CA1822
    public BlockSubtreeWithAnchor[] Apply(
#pragma warning restore CA1822
        BlockSubtreeWithAnchor[] tree,
        IReadOnlyList<PatchOperation> operations)
    {
        var current = tree;
        foreach (var op in operations)
            current = Dispatch(current, op);
        return current;
    }

    private static BlockSubtreeWithAnchor[] Dispatch(
        BlockSubtreeWithAnchor[] tree,
        PatchOperation op) => op switch
        {
            ReplaceBlockOperation r => ApplyReplaceBlock(tree, r),
            ReplaceSectionOperation r => ApplyReplaceSection(tree, r),
            SearchReplaceOperation s => ApplySearchReplace(tree, s),
            AppendSectionOperation a => ApplyAppendSection(tree, a),
            InsertAfterBlockOperation i => ApplyInsertAfterBlock(tree, i),
            _ => tree
        };

    private static BlockSubtreeWithAnchor[] ApplyReplaceBlock(
        BlockSubtreeWithAnchor[] tree,
        ReplaceBlockOperation op)
    {
        var parsed = AnchoredMarkdownParser.Parse(op.Markdown);

        if (op.Anchor == "root")
        {
            return [..tree.Select(n =>
                n.AnchorKind == AnchorKind.Root
                    ? n with { Children = parsed }
                    : n)];
        }

        var node = FindNode(tree, op.Anchor)
            ?? throw new UnknownAnchorException(op.Anchor);

        if (node.AnchorKind == AnchorKind.Opaque)
            throw new UnsupportedBlockTouchedException(op.Anchor);

        // Preserve original anchor ID for the first replacement block so Reconciler
        // produces an Update op rather than Delete + Append.
        var adjusted = parsed.Count == 0
            ? (IReadOnlyList<BlockSubtreeWithAnchor>)parsed
            : [parsed[0] with { AnchorId = op.Anchor, AnchorKind = AnchorKind.Block }, ..parsed.Skip(1)];

        return SwapNode(tree, op.Anchor, adjusted)
            ?? tree;
    }

    private static BlockSubtreeWithAnchor[] ApplyReplaceSection(
        BlockSubtreeWithAnchor[] tree,
        ReplaceSectionOperation op)
    {
        var node = FindNode(tree, op.Anchor)
            ?? throw new UnknownAnchorException(op.Anchor);

        if (!IsHeading(node))
            throw new SectionAnchorNotHeadingException(op.Anchor);

        var level = HeadingLevel(node);
        var parsed = AnchoredMarkdownParser.Parse(op.Markdown);

        return SwapSection(tree, op.Anchor, level, parsed)
            ?? tree;
    }

    private static BlockSubtreeWithAnchor[] ApplySearchReplace(
        BlockSubtreeWithAnchor[] tree,
        SearchReplaceOperation op)
    {
        var md = AnchoredTreeSerializer.SerializeTree(tree);
        var idx = md.IndexOf(op.OldStr, StringComparison.Ordinal);

        if (idx < 0)
            throw new NoMatchException(op.OldStr);

        var second = md.IndexOf(op.OldStr, idx + op.OldStr.Length, StringComparison.Ordinal);
        if (second >= 0)
        {
            var count = 2;
            var pos = second + op.OldStr.Length;
            while (md.IndexOf(op.OldStr, pos, StringComparison.Ordinal) is >= 0 and var next)
            {
                count++;
                pos = next + op.OldStr.Length;
            }
            throw new AmbiguousMatchException(op.OldStr, count);
        }

        var replaced = string.Concat(
            md.AsSpan(0, idx),
            op.NewStr,
            md.AsSpan(idx + op.OldStr.Length));

        var parsed = AnchoredMarkdownParser.Parse(replaced);

        if (parsed.Any(p => p.AnchorKind == AnchorKind.Root))
            return parsed.ToArray();

        if (!tree.Any(n => n.AnchorKind == AnchorKind.Root))
            return parsed.ToArray();

        return [..tree.Select(n =>
            n.AnchorKind == AnchorKind.Root
                ? n with { Children = parsed }
                : n)];
    }

    private static BlockSubtreeWithAnchor[] ApplyAppendSection(
        BlockSubtreeWithAnchor[] tree,
        AppendSectionOperation op)
    {
        var parsed = AnchoredMarkdownParser.Parse(op.Markdown);

        if (op.Anchor is null or "root")
        {
            return [..tree.Select(n =>
                n.AnchorKind == AnchorKind.Root
                    ? n with { Children = [..n.Children, ..parsed] }
                    : n)];
        }

        var node = FindNode(tree, op.Anchor)
            ?? throw new UnknownAnchorException(op.Anchor);

        if (!IsContainer(node))
            throw new AnchorNotContainerException(
                op.Anchor, node.Block?.Block.Type ?? "unknown");

        return AppendToContainer(tree, op.Anchor, parsed)
            ?? tree;
    }

    private static BlockSubtreeWithAnchor[] ApplyInsertAfterBlock(
        BlockSubtreeWithAnchor[] tree,
        InsertAfterBlockOperation op)
    {
        if (op.Anchor == "root")
            throw new UnknownAnchorException("root");

        var node = FindNode(tree, op.Anchor)
            ?? throw new UnknownAnchorException(op.Anchor);

        if (node.AnchorKind == AnchorKind.Opaque)
            throw new UnsupportedBlockTouchedException(op.Anchor);

        var parsed = AnchoredMarkdownParser.Parse(op.Markdown);

        return InsertAfter(tree, op.Anchor, parsed)
            ?? tree;
    }

    private static BlockSubtreeWithAnchor? FindNode(
        IReadOnlyList<BlockSubtreeWithAnchor> nodes,
        string anchor)
    {
        foreach (var n in nodes)
        {
            if (n.AnchorId == anchor)
                return n;
            var found = FindNode(n.Children, anchor);
            if (found is not null)
                return found;
        }
        return null;
    }

    private static BlockSubtreeWithAnchor[]? SwapNode(
        IReadOnlyList<BlockSubtreeWithAnchor> siblings,
        string anchor,
        IReadOnlyList<BlockSubtreeWithAnchor> replacements)
    {
        for (int i = 0; i < siblings.Count; i++)
        {
            if (siblings[i].AnchorId == anchor)
            {
                var list = siblings.ToList();
                list.RemoveAt(i);
                list.InsertRange(i, replacements);
                return list.ToArray();
            }

            var childResult = SwapNode(siblings[i].Children, anchor, replacements);
            if (childResult is not null)
            {
                var list = siblings.ToList();
                list[i] = siblings[i] with { Children = childResult };
                return list.ToArray();
            }
        }
        return null;
    }

    private static BlockSubtreeWithAnchor[]? SwapSection(
        IReadOnlyList<BlockSubtreeWithAnchor> siblings,
        string anchor,
        int headingLevel,
        IReadOnlyList<BlockSubtreeWithAnchor> replacements)
    {
        int headingIdx = -1;
        for (int i = 0; i < siblings.Count; i++)
        {
            if (siblings[i].AnchorId == anchor)
            {
                headingIdx = i;
                break;
            }

            var childResult = SwapSection(
                siblings[i].Children, anchor, headingLevel, replacements);
            if (childResult is not null)
            {
                var list = siblings.ToList();
                list[i] = siblings[i] with { Children = childResult };
                return list.ToArray();
            }
        }

        if (headingIdx < 0)
            return null;

        int end = headingIdx + 1;
        while (end < siblings.Count)
        {
            if (IsHeading(siblings[end]) && HeadingLevel(siblings[end]) <= headingLevel)
                break;
            end++;
        }

        for (int i = headingIdx; i < end; i++)
        {
            if (siblings[i].AnchorKind == AnchorKind.Opaque)
                throw new UnsupportedBlockTouchedException(siblings[i].AnchorId!);
        }

        var newList = siblings.ToList();
        newList.RemoveRange(headingIdx, end - headingIdx);
        newList.InsertRange(headingIdx, replacements);
        return newList.ToArray();
    }

    private static BlockSubtreeWithAnchor[]? AppendToContainer(
        IReadOnlyList<BlockSubtreeWithAnchor> siblings,
        string anchor,
        IReadOnlyList<BlockSubtreeWithAnchor> additions)
    {
        for (int i = 0; i < siblings.Count; i++)
        {
            if (siblings[i].AnchorId == anchor)
            {
                var list = siblings.ToList();
                list[i] = siblings[i] with
                {
                    Children = [..siblings[i].Children, ..additions]
                };
                return list.ToArray();
            }

            var childResult = AppendToContainer(siblings[i].Children, anchor, additions);
            if (childResult is not null)
            {
                var list = siblings.ToList();
                list[i] = siblings[i] with { Children = childResult };
                return list.ToArray();
            }
        }
        return null;
    }

    private static BlockSubtreeWithAnchor[]? InsertAfter(
        IReadOnlyList<BlockSubtreeWithAnchor> siblings,
        string anchor,
        IReadOnlyList<BlockSubtreeWithAnchor> additions)
    {
        for (int i = 0; i < siblings.Count; i++)
        {
            if (siblings[i].AnchorId == anchor)
            {
                var list = siblings.ToList();
                list.InsertRange(i + 1, additions);
                return list.ToArray();
            }

            var childResult = InsertAfter(siblings[i].Children, anchor, additions);
            if (childResult is not null)
            {
                var list = siblings.ToList();
                list[i] = siblings[i] with { Children = childResult };
                return list.ToArray();
            }
        }
        return null;
    }

    private static bool IsHeading(BlockSubtreeWithAnchor node)
    {
        return node.Block?.Block is Heading1Block or Heading2Block or Heading3Block;
    }

    private static int HeadingLevel(BlockSubtreeWithAnchor node)
    {
        return node.Block?.Block switch
        {
            Heading1Block => 1,
            Heading2Block => 2,
            Heading3Block => 3,
            _ => int.MaxValue
        };
    }

    private static bool IsContainer(BlockSubtreeWithAnchor node)
    {
        if (node.AnchorKind == AnchorKind.Root)
            return true;

        if (node.Block?.Block is BulletedListItemBlock
            or NumberedListItemBlock
            or ToDoBlock
            or QuoteBlock)
            return true;

        return node.Children.Count > 0;
    }
}
