using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;

namespace Buildout.Core.Markdown.Authoring.Blocks;

public sealed class UnsupportedBlockPlaceholderPassThrough : IMarkdownBlockParser
{
    public bool CanParse(Markdig.Syntax.Block block)
    {
        if (block is HtmlBlock html)
        {
            var lines = html.Lines.ToString();
            return lines.Contains("<!-- unsupported block:");
        }
        return false;
    }

    public BlockSubtreeWrite Parse(Markdig.Syntax.Block block, IInlineMarkdownParser inlineParser)
    {
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
