using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;

namespace Buildout.Core.Markdown.Authoring.Blocks;

public sealed class CodeBlockParser : IMarkdownBlockParser
{
    public bool CanParse(Markdig.Syntax.Block block) => block is FencedCodeBlock;

    public BlockSubtreeWrite Parse(Markdig.Syntax.Block block, IInlineMarkdownParser inlineParser)
    {
        var code = (FencedCodeBlock)block;
        var text = code.Lines.ToString();
        var language = string.IsNullOrEmpty(code.Info) ? null : code.Info;

        return new BlockSubtreeWrite
        {
            Block = new Buildin.Models.CodeBlock
            {
                RichTextContent = [new RichText { Type = "text", Content = text }],
                Language = language
            },
            Children = []
        };
    }
}
