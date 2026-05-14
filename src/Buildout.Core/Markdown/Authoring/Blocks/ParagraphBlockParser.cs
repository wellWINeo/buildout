using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;

namespace Buildout.Core.Markdown.Authoring.Blocks;

public sealed class ParagraphBlockParser : IMarkdownBlockParser
{
    public bool CanParse(Markdig.Syntax.Block block) => block is Markdig.Syntax.ParagraphBlock;

    public BlockSubtreeWrite Parse(Markdig.Syntax.Block block, IInlineMarkdownParser inlineParser)
    {
        var para = (Markdig.Syntax.ParagraphBlock)block;
        var richTexts = para.Inline is not null
            ? inlineParser.ParseInlines(para.Inline).ToArray()
            : Array.Empty<RichText>();

        return new BlockSubtreeWrite
        {
            Block = new Buildin.Models.ParagraphBlock { RichTextContent = richTexts },
            Children = []
        };
    }
}
