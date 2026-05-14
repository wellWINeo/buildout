using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;

namespace Buildout.Core.Markdown.Authoring.Blocks;

public sealed class HeadingBlockParser : IMarkdownBlockParser
{
    public bool CanParse(Markdig.Syntax.Block block) => block is Markdig.Syntax.HeadingBlock;

    public BlockSubtreeWrite Parse(Markdig.Syntax.Block block, IInlineMarkdownParser inlineParser)
    {
        var heading = (Markdig.Syntax.HeadingBlock)block;
        var richTexts = heading.Inline is not null
            ? inlineParser.ParseInlines(heading.Inline).ToArray()
            : Array.Empty<RichText>();

        Buildin.Models.Block buildinBlock = heading.Level switch
        {
            1 => new Heading1Block { RichTextContent = richTexts },
            2 => new Heading2Block { RichTextContent = richTexts },
            3 => new Heading3Block { RichTextContent = richTexts },
            _ => new Buildin.Models.ParagraphBlock
            {
                RichTextContent = [new RichText { Type = "text", Content = new string('#', heading.Level) + " " + string.Join("", richTexts.Select(r => r.Content)) }]
            }
        };

        return new BlockSubtreeWrite
        {
            Block = buildinBlock,
            Children = []
        };
    }
}
