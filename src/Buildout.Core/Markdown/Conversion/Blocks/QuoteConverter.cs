using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Internal;

namespace Buildout.Core.Markdown.Conversion.Blocks;

internal sealed class QuoteConverter : IBlockToMarkdownConverter
{
    public Type BlockClrType => typeof(QuoteBlock);
    public string BlockType => "quote";
    public bool RecurseChildren => true;

    public void Write(Block block, IReadOnlyList<BlockSubtree> children, IMarkdownRenderContext ctx)
    {
        var q = (QuoteBlock)block;
        var inline = ctx.Inline.Render(q.RichTextContent, ctx.IndentLevel);
        var lines = string.IsNullOrEmpty(inline) ? [] : inline.Split('\n');
        foreach (var line in lines)
            ctx.Writer.WriteLine($"> {line}");
        ctx.Writer.WriteBlankLine();
        foreach (var child in children)
            ctx.WriteBlockSubtree(child);
    }
}
