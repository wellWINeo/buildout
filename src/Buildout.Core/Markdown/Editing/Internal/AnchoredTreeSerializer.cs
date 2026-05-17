using System.Collections.Generic;
using System.Text;
using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Editing.Internal;

internal static class AnchoredTreeSerializer
{
    public static string SerializeTree(IReadOnlyList<BlockSubtreeWithAnchor> nodes)
    {
        var sb = new StringBuilder();
        foreach (var node in nodes)
            SerializeNode(sb, node);
        return sb.ToString();
    }

    private static void SerializeNode(StringBuilder sb, BlockSubtreeWithAnchor node)
    {
        if (node.AnchorKind == AnchorKind.Root)
        {
            sb.AppendLine("<!-- buildin:root -->");
            sb.AppendLine();
            foreach (var child in node.Children)
                SerializeNode(sb, child);
            return;
        }

        if (node.AnchorKind == AnchorKind.Opaque && node.AnchorId is not null)
            sb.Append("<!-- buildin:opaque:").Append(node.AnchorId).AppendLine(" -->");
        else if (node.AnchorId is not null)
            sb.Append("<!-- buildin:block:").Append(node.AnchorId).AppendLine(" -->");

        var block = node.Block?.Block;
        switch (block)
        {
            case Heading1Block h:
                sb.Append("# ").AppendLine(RenderInlineText(h.RichTextContent));
                break;
            case Heading2Block h:
                sb.Append("## ").AppendLine(RenderInlineText(h.RichTextContent));
                break;
            case Heading3Block h:
                sb.Append("### ").AppendLine(RenderInlineText(h.RichTextContent));
                break;
            case ParagraphBlock p:
                sb.AppendLine(RenderInlineText(p.RichTextContent));
                break;
            case BulletedListItemBlock b:
                sb.Append("- ").AppendLine(RenderInlineText(b.RichTextContent));
                break;
            case NumberedListItemBlock n:
                sb.Append("1. ").AppendLine(RenderInlineText(n.RichTextContent));
                break;
            case ToDoBlock t:
                sb.Append("- [").Append(t.Checked == true ? "x" : " ").Append("] ").AppendLine(RenderInlineText(t.RichTextContent));
                break;
            case CodeBlock c:
                sb.Append("```").AppendLine(c.Language ?? string.Empty);
                sb.AppendLine(RenderInlineText(c.RichTextContent));
                sb.AppendLine("```");
                break;
            case QuoteBlock q:
                sb.Append("> ").AppendLine(RenderInlineText(q.RichTextContent));
                break;
            case DividerBlock:
                sb.AppendLine("---");
                break;
        }

        sb.AppendLine();

        foreach (var child in node.Children)
            SerializeNode(sb, child);
    }

    private static string RenderInlineText(IReadOnlyList<RichText>? items)
    {
        if (items is null or { Count: 0 })
            return string.Empty;
        var sb = new StringBuilder();
        foreach (var item in items)
            sb.Append(item.Content);
        return sb.ToString();
    }
}
