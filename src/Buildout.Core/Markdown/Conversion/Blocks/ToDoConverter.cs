using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Internal;

namespace Buildout.Core.Markdown.Conversion.Blocks;

internal sealed class ToDoConverter : IBlockToMarkdownConverter
{
    public Type BlockClrType => typeof(ToDoBlock);
    public string BlockType => "to_do";
    public bool RecurseChildren => true;

    public void Write(Block block, IReadOnlyList<BlockSubtree> children, IMarkdownRenderContext ctx)
    {
        var item = (ToDoBlock)block;
        var indent = new string(' ', ctx.IndentLevel * 2);
        var check = item.Checked == true ? "x" : " ";
        var inline = ctx.Inline.Render(item.RichTextContent, ctx.IndentLevel);
        ctx.Writer.WriteLine($"{indent}- [{check}] {inline}");

        foreach (var child in children)
            ctx.WriteBlockSubtree(child);
    }
}
