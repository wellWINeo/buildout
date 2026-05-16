using System.Text.RegularExpressions;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Markdown.Authoring.Blocks;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig;
using Markdig.Syntax;

namespace Buildout.Core.Markdown.Editing.Internal;

public static class AnchoredMarkdownParser
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseTaskLists()
        .Build();

    private static readonly Regex RootPattern = new(@"<!--\s*buildin:root\s*-->", RegexOptions.Compiled);
    private static readonly Regex BlockAnchorPattern = new(@"<!--\s*buildin:block:(\S+?)\s*-->", RegexOptions.Compiled);
    private static readonly Regex OpaqueAnchorPattern = new(@"<!--\s*buildin:opaque:(\S+?)\s*-->", RegexOptions.Compiled);

    public static IReadOnlyList<BlockSubtreeWithAnchor> Parse(string markdown)
    {
        var doc = Markdig.Markdown.Parse(markdown, Pipeline);
        var inlineParser = new InlineMarkdownParser();
        var paragraphParser = new ParagraphBlockParser();
        var blockParsers = new IMarkdownBlockParser[]
        {
            new HeadingBlockParser(),
            new CodeBlockParser(),
            new QuoteBlockParser(),
            new DividerBlockParser(),
            new UnsupportedBlockPlaceholderPassThrough(),
            paragraphParser,
        };
        var toDoParser = new ToDoBlockParser();
        var bulletParser = new BulletedListItemBlockParser();
        var numberedParser = new NumberedListItemBlockParser();

        var entries = new List<(AnchorKind? Kind, string? Id, BlockSubtreeWrite Write)>();
        AnchorKind? pendingKind = null;
        string? pendingId = null;

        foreach (var block in doc)
        {
            if (block is HtmlBlock html)
            {
                var text = html.Lines.ToString();
                var anchor = TryParseAnchor(text);
                if (anchor is not null)
                {
                    pendingKind = anchor.Value.Kind;
                    pendingId = anchor.Value.Id;
                    continue;
                }
            }

            foreach (var write in DispatchBlock(block, inlineParser, blockParsers, paragraphParser, toDoParser, bulletParser, numberedParser))
            {
                entries.Add((pendingKind, pendingId, write));
            }

            pendingKind = null;
            pendingId = null;
        }

        return BuildTree(entries);
    }

    private static (AnchorKind Kind, string Id)? TryParseAnchor(string text)
    {
        if (RootPattern.IsMatch(text))
            return (AnchorKind.Root, "root");
        var m = BlockAnchorPattern.Match(text);
        if (m.Success)
            return (AnchorKind.Block, m.Groups[1].Value);
        m = OpaqueAnchorPattern.Match(text);
        if (m.Success)
            return (AnchorKind.Opaque, m.Groups[1].Value);
        return null;
    }

    private static List<BlockSubtreeWrite> DispatchBlock(
        Markdig.Syntax.Block block,
        InlineMarkdownParser inlineParser,
        IReadOnlyList<IMarkdownBlockParser> blockParsers,
        ParagraphBlockParser paragraphParser,
        ToDoBlockParser toDoParser,
        BulletedListItemBlockParser bulletParser,
        NumberedListItemBlockParser numberedParser)
    {
        if (block is ListBlock list)
            return DispatchList(list, inlineParser, toDoParser, bulletParser, numberedParser);

        if (block is HtmlBlock htmlBlock)
        {
            var text = htmlBlock.Lines.ToString();
            return [new BlockSubtreeWrite
            {
                Block = new Buildin.Models.ParagraphBlock { RichTextContent = [new RichText { Type = "text", Content = text.Trim() }] },
                Children = []
            }];
        }

        foreach (var parser in blockParsers)
        {
            if (parser.CanParse(block))
                return [parser.Parse(block, inlineParser)];
        }

        return [paragraphParser.Parse(block, inlineParser)];
    }

    private static List<BlockSubtreeWrite> DispatchList(
        ListBlock list,
        InlineMarkdownParser inlineParser,
        ToDoBlockParser toDoParser,
        BulletedListItemBlockParser bulletParser,
        NumberedListItemBlockParser numberedParser)
    {
        var items = new List<BlockSubtreeWrite>();
        foreach (ListItemBlock item in list)
        {
            if (list.IsOrdered)
                items.Add(numberedParser.Parse(item, inlineParser));
            else if (toDoParser.CanParse(item))
                items.Add(toDoParser.Parse(item, inlineParser));
            else
                items.Add(bulletParser.Parse(item, inlineParser));
        }
        return items;
    }

    private static List<BlockSubtreeWithAnchor> BuildTree(
        List<(AnchorKind? Kind, string? Id, BlockSubtreeWrite Write)> entries)
    {
        var result = new List<BlockSubtreeWithAnchor>();
        BlockSubtreeWithAnchor? currentRoot = null;
        var rootChildren = new List<BlockSubtreeWithAnchor>();

        for (int i = 0; i < entries.Count; i++)
        {
            var (kind, id, write) = entries[i];

            if (kind == AnchorKind.Root)
            {
                if (currentRoot is not null)
                {
                    result.Add(currentRoot with { Children = rootChildren.ToArray() });
                    rootChildren = [];
                }

                currentRoot = new BlockSubtreeWithAnchor
                {
                    AnchorId = id,
                    AnchorKind = AnchorKind.Root,
                    Block = write,
                    Children = []
                };
            }
            else if (currentRoot is not null)
            {
                rootChildren.Add(new BlockSubtreeWithAnchor
                {
                    AnchorId = id,
                    AnchorKind = kind ?? AnchorKind.Block,
                    Block = write,
                    Children = []
                });
            }
            else
            {
                result.Add(new BlockSubtreeWithAnchor
                {
                    AnchorId = id,
                    AnchorKind = kind ?? AnchorKind.Block,
                    Block = write,
                    Children = []
                });
            }
        }

        if (currentRoot is not null)
            result.Add(currentRoot with { Children = rootChildren.ToArray() });

        return result;
    }
}
