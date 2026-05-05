using System.Text;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Internal;

namespace Buildout.Core.Markdown.Conversion;

public sealed class CodeConverter : IBlockToMarkdownConverter
{
    public Type BlockClrType => typeof(CodeBlock);
    public string BlockType => "code";
    public bool RecurseChildren => false;

    public void Write(Block block, IReadOnlyList<BlockSubtree> children, IMarkdownRenderContext ctx)
    {
        var codeBlock = (CodeBlock)block;
        var text = BuildPlainText(codeBlock.RichTextContent);
        var fence = string.IsNullOrEmpty(codeBlock.Language)
            ? "```"
            : $"```{codeBlock.Language}";

        ctx.Writer.WriteLine(fence);
        ctx.Writer.WriteLine(text);
        ctx.Writer.WriteLine("```");
        ctx.Writer.WriteBlankLine();
    }

    private static string BuildPlainText(IReadOnlyList<RichText>? items)
    {
        if (items is null or { Count: 0 })
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var item in items)
            sb.Append(item.Content);

        return sb.ToString();
    }
}
