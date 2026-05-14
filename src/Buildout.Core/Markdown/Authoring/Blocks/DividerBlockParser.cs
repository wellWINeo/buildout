using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;

namespace Buildout.Core.Markdown.Authoring.Blocks;

public sealed class DividerBlockParser : IMarkdownBlockParser
{
    public bool CanParse(Markdig.Syntax.Block block) => block is ThematicBreakBlock;

    public BlockSubtreeWrite Parse(Markdig.Syntax.Block block, IInlineMarkdownParser inlineParser)
    {
        return new BlockSubtreeWrite
        {
            Block = new DividerBlock(),
            Children = []
        };
    }
}
