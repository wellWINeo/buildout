using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;
using MarkdigQuoteBlock = Markdig.Syntax.QuoteBlock;

namespace Buildout.Core.Markdown.Authoring.Blocks;

public sealed class QuoteBlockParser : IMarkdownBlockParser
{
    public bool CanParse(Markdig.Syntax.Block block) => block is MarkdigQuoteBlock;

    public BlockSubtreeWrite Parse(Markdig.Syntax.Block block, IInlineMarkdownParser inlineParser)
    {
        var quote = (MarkdigQuoteBlock)block;
        var richTexts = new List<RichText>();

        foreach (var child in quote)
        {
            if (child is Markdig.Syntax.ParagraphBlock para && para.Inline is not null)
            {
                richTexts.AddRange(inlineParser.ParseInlines(para.Inline));
            }
        }

        return new BlockSubtreeWrite
        {
            Block = new Buildout.Core.Buildin.Models.QuoteBlock { RichTextContent = richTexts },
            Children = []
        };
    }
}
