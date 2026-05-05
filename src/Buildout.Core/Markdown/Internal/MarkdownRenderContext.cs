using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;

namespace Buildout.Core.Markdown.Internal;

internal sealed class MarkdownRenderContext : IMarkdownRenderContext
{
    private readonly BlockToMarkdownRegistry _registry;

    public IMarkdownWriter Writer { get; }
    public IInlineRenderer Inline { get; }
    public int IndentLevel { get; }

    public MarkdownRenderContext(
        IMarkdownWriter writer,
        IInlineRenderer inline,
        BlockToMarkdownRegistry registry,
        int indentLevel)
    {
        Writer = writer;
        Inline = inline;
        _registry = registry;
        IndentLevel = indentLevel;
    }

    public IMarkdownRenderContext WithIndent(int delta)
        => new MarkdownRenderContext(Writer, Inline, _registry, IndentLevel + delta);

    public void WriteBlockSubtree(BlockSubtree subtree)
    {
        var converter = _registry.Resolve(subtree.Block);
        if (converter is not null)
            converter.Write(subtree.Block, subtree.Children, this);
        else
            UnsupportedBlockHandler.Write(subtree.Block, this);
    }
}
