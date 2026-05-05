using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Conversion;

public static class UnsupportedBlockHandler
{
    public static void Write(Block block, IMarkdownRenderContext ctx)
    {
        ctx.Writer.WriteLine($"<!-- unsupported block: {block.Type} -->");
        ctx.Writer.WriteBlankLine();
    }
}
