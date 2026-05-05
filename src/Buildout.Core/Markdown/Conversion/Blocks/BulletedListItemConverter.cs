using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Internal;

namespace Buildout.Core.Markdown.Conversion.Blocks;

internal sealed class BulletedListItemConverter : IBlockToMarkdownConverter
{
    public Type BlockClrType => typeof(BulletedListItemBlock);
    public string BlockType => "bulleted_list_item";
    public bool RecurseChildren => true;

    public void Write(Block block, IReadOnlyList<BlockSubtree> children, IMarkdownRenderContext ctx)
    {
        var item = (BulletedListItemBlock)block;
        var indent = new string(' ', ctx.IndentLevel * 2);
        var inline = ctx.Inline.Render(item.RichTextContent, ctx.IndentLevel);
        ctx.Writer.WriteLine(indent + "- " + inline);

        foreach (var child in children)
            ctx.WriteBlockSubtree(child);
    }
}
