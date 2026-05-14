using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;

namespace Buildout.Core.Markdown.Authoring.Blocks;

public sealed class UnsupportedBlockPlaceholderPassThrough
{
    public static bool IsPlaceholder(Markdig.Syntax.Block block)
    {
        if (block is HtmlBlock html)
        {
            var lines = html.Lines.ToString();
            return lines.Contains("<!-- unsupported block:");
        }
        return false;
    }

    public static BlockSubtreeWrite? TryParse(Markdig.Syntax.Block block, IInlineMarkdownParser inlineParser)
    {
        if (!IsPlaceholder(block)) return null;
        var html = (HtmlBlock)block;
        var text = html.Lines.ToString();
        return new BlockSubtreeWrite
        {
            Block = new Buildin.Models.ParagraphBlock
            {
                RichTextContent = [new RichText { Type = "text", Content = text.Trim() }]
            },
            Children = []
        };
    }
}
