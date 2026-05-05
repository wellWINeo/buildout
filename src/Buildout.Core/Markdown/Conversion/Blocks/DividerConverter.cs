using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Internal;

namespace Buildout.Core.Markdown.Conversion.Blocks;

internal sealed class DividerConverter : IBlockToMarkdownConverter
{
    public Type BlockClrType => typeof(DividerBlock);
    public string BlockType => "divider";
    public bool RecurseChildren => false;

    public void Write(Block block, IReadOnlyList<BlockSubtree> children, IMarkdownRenderContext ctx)
    {
        ctx.Writer.WriteLine("---");
        ctx.Writer.WriteBlankLine();
    }
}
