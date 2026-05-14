using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;

namespace Buildout.Core.Markdown.Authoring;

public interface IMarkdownBlockParser
{
    bool CanParse(Block block);
    BlockSubtreeWrite Parse(Block block, IInlineMarkdownParser inlineParser);
}
