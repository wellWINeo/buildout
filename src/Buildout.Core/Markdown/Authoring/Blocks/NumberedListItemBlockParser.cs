using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;

namespace Buildout.Core.Markdown.Authoring.Blocks;

public sealed class NumberedListItemBlockParser : IMarkdownBlockParser
{
    public bool CanParse(Markdig.Syntax.Block block) => block is ListItemBlock { Parent: ListBlock { IsOrdered: true } };

    public BlockSubtreeWrite Parse(Markdig.Syntax.Block block, IInlineMarkdownParser inlineParser)
    {
        var item = (ListItemBlock)block;
        return ParseItem(item, inlineParser);
    }

    internal static BlockSubtreeWrite ParseItem(ListItemBlock item, IInlineMarkdownParser inlineParser)
    {
        var richTexts = new List<RichText>();
        var children = new List<BlockSubtreeWrite>();

        foreach (var child in item)
        {
            switch (child)
            {
                case Markdig.Syntax.ParagraphBlock para when para.Inline is not null:
                    richTexts.AddRange(inlineParser.ParseInlines(para.Inline));
                    break;
                case ListBlock nestedList:
                    children.AddRange(ParseNestedList(nestedList, inlineParser));
                    break;
            }
        }

        return new BlockSubtreeWrite
        {
            Block = new NumberedListItemBlock { RichTextContent = richTexts },
            Children = children
        };
    }

    private static List<BlockSubtreeWrite> ParseNestedList(ListBlock list, IInlineMarkdownParser inlineParser)
    {
        var items = new List<BlockSubtreeWrite>();
        foreach (ListItemBlock item in list)
        {
            if (list.IsOrdered)
            {
                items.Add(ParseItem(item, inlineParser));
            }
            else
            {
                items.Add(BulletedListItemBlockParser.ParseItem(item, inlineParser));
            }
        }
        return items;
    }
}
