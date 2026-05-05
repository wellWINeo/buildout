using Buildout.Core.Markdown.Internal;

namespace Buildout.Core.Markdown.Conversion;

public interface IMarkdownRenderContext
{
    IMarkdownWriter Writer { get; }
    IInlineRenderer Inline { get; }
    int IndentLevel { get; }
    IMarkdownRenderContext WithIndent(int delta);
    void WriteBlockSubtree(BlockSubtree subtree);
}
