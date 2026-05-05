using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Internal;

namespace Buildout.Core.Markdown.Conversion.Blocks;

internal sealed class ParagraphConverter : IBlockToMarkdownConverter
{
    public Type BlockClrType => typeof(ParagraphBlock);
    public string BlockType => "paragraph";
    public bool RecurseChildren => false;

    public void Write(Block block, IReadOnlyList<BlockSubtree> children, IMarkdownRenderContext ctx)
    {
        var p = (ParagraphBlock)block;
        var inline = ctx.Inline.Render(p.RichTextContent, ctx.IndentLevel);
        ctx.Writer.WriteLine(inline);
        ctx.Writer.WriteBlankLine();
    }
}
