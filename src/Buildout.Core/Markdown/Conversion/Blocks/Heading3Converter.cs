using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Internal;

namespace Buildout.Core.Markdown.Conversion.Blocks;

internal sealed class Heading3Converter : IBlockToMarkdownConverter
{
    public Type BlockClrType => typeof(Heading3Block);
    public string BlockType => "heading_3";
    public bool RecurseChildren => false;

    public void Write(Block block, IReadOnlyList<BlockSubtree> children, IMarkdownRenderContext ctx)
    {
        var h = (Heading3Block)block;
        var inline = ctx.Inline.Render(h.RichTextContent, ctx.IndentLevel);
        ctx.Writer.WriteLine("#### " + inline);
        ctx.Writer.WriteBlankLine();
    }
}
