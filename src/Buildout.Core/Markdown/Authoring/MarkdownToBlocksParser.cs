using Buildout.Core.Markdown.Authoring.Blocks;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig;
using Markdig.Syntax;

namespace Buildout.Core.Markdown.Authoring;

public sealed class MarkdownToBlocksParser : IMarkdownToBlocksParser
{
    private readonly MarkdownPipeline _pipeline;
    private readonly InlineMarkdownParser _inlineParser;
    private readonly ToDoBlockParser _toDoParser = new();
    private readonly BulletedListItemBlockParser _bulletParser = new();
    private readonly NumberedListItemBlockParser _numberedParser = new();
    private readonly HeadingBlockParser _headingParser = new();
    private readonly ParagraphBlockParser _paragraphParser = new();
    private readonly CodeBlockParser _codeParser = new();
    private readonly QuoteBlockParser _quoteParser = new();
    private readonly DividerBlockParser _dividerParser = new();

    public MarkdownToBlocksParser()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseTaskLists()
            .Build();
        _inlineParser = new InlineMarkdownParser();
    }

    public AuthoredDocument Parse(string markdown)
    {
        var doc = Markdig.Markdown.Parse(markdown, _pipeline);
        var (title, doc2) = TitleExtractor.Extract(doc, _inlineParser);

        var body = new List<BlockSubtreeWrite>();
        foreach (var block in doc2)
        {
            foreach (var subtree in DispatchBlock(block))
            {
                body.Add(subtree);
            }
        }

        return new AuthoredDocument { Title = title, Body = body };
    }

    private List<BlockSubtreeWrite> DispatchBlock(Markdig.Syntax.Block block)
    {
        switch (block)
        {
            case Markdig.Syntax.ParagraphBlock:
                return [_paragraphParser.Parse(block, _inlineParser)];

            case Markdig.Syntax.HeadingBlock:
                return [_headingParser.Parse(block, _inlineParser)];

            case ListBlock list:
                return DispatchList(list);

            case FencedCodeBlock:
                return [_codeParser.Parse(block, _inlineParser)];

            case QuoteBlock:
                return [_quoteParser.Parse(block, _inlineParser)];

            case ThematicBreakBlock:
                return [_dividerParser.Parse(block, _inlineParser)];

            case HtmlBlock:
                return [DispatchHtmlBlock(block)];

            default:
                return [_paragraphParser.Parse(block, _inlineParser)];
        }
    }

    private List<BlockSubtreeWrite> DispatchList(ListBlock list)
    {
        var items = new List<BlockSubtreeWrite>();
        foreach (ListItemBlock item in list)
        {
            if (list.IsOrdered)
            {
                items.Add(_numberedParser.Parse(item, _inlineParser));
            }
            else
            {
                if (_toDoParser.CanParse(item))
                {
                    items.Add(_toDoParser.Parse(item, _inlineParser));
                }
                else
                {
                    items.Add(_bulletParser.Parse(item, _inlineParser));
                }
            }
        }
        return items;
    }

    private BlockSubtreeWrite DispatchHtmlBlock(Markdig.Syntax.Block block)
    {
        var placeholder = UnsupportedBlockPlaceholderPassThrough.TryParse(block, _inlineParser);
        if (placeholder is not null)
            return placeholder;

        return _paragraphParser.Parse(block, _inlineParser);
    }
}
