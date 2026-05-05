using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Internal;

namespace Buildout.Core.Markdown.Conversion.Blocks;

internal sealed class Heading1Converter : IBlockToMarkdownConverter
{
    public Type BlockClrType => typeof(Heading1Block);
    public string BlockType => "heading_1";
    public bool RecurseChildren => false;

    public void Write(Block block, IReadOnlyList<BlockSubtree> children, IMarkdownRenderContext ctx)
    {
        var h = (Heading1Block)block;
        var inline = ctx.Inline.Render(h.RichTextContent, ctx.IndentLevel);
        ctx.Writer.WriteLine("## " + inline);
        ctx.Writer.WriteBlankLine();
    }
}
