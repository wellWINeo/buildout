using Buildout.Core.Diagnostics;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace Buildout.Core.Markdown.Editing.Internal;

public sealed class AnchoredMarkdownRenderer
{
    private readonly IInlineRenderer _inlineRenderer;
    private readonly BlockToMarkdownRegistry _registry;

    public AnchoredMarkdownRenderer(IInlineRenderer inlineRenderer, BlockToMarkdownRegistry registry)
    {
        _inlineRenderer = inlineRenderer;
        _registry = registry;
    }

    public (string Markdown, IReadOnlyList<string> UnknownBlockIds) Render(IReadOnlyList<BlockSubtree> tree)
    {
        using var recorder = OperationRecorder.Start(NullLogger.Instance, "page_read_editing");

        var writer = new MarkdownWriter();
        var unknownIds = new List<string>();

        writer.WriteLine("<!-- buildin:root -->");
        writer.WriteBlankLine();

        var ctx = new AnchoredRenderContext(writer, _inlineRenderer, _registry, 0, 0, unknownIds);
        foreach (var subtree in tree)
            ctx.WriteBlockSubtree(subtree);

        recorder.Succeed();

        return (writer.ToString(), unknownIds);
    }

    private sealed class AnchoredRenderContext : IMarkdownRenderContext
    {
        private readonly BlockToMarkdownRegistry _registry;
        private readonly List<string> _unknownIds;
        private readonly int _anchorDepth;

        public IMarkdownWriter Writer { get; }
        public IInlineRenderer Inline { get; }
        public int IndentLevel { get; }

        public AnchoredRenderContext(
            IMarkdownWriter writer,
            IInlineRenderer inline,
            BlockToMarkdownRegistry registry,
            int indentLevel,
            int anchorDepth,
            List<string> unknownIds)
        {
            Writer = writer;
            Inline = inline;
            _registry = registry;
            IndentLevel = indentLevel;
            _anchorDepth = anchorDepth;
            _unknownIds = unknownIds;
        }

        public IMarkdownRenderContext WithIndent(int delta)
            => new AnchoredRenderContext(Writer, Inline, _registry, IndentLevel + delta, _anchorDepth, _unknownIds);

        public void WriteBlockSubtree(BlockSubtree subtree)
        {
            var block = subtree.Block;
            var converter = _registry.Resolve(block);
            var anchorIndent = new string(' ', _anchorDepth * 2);

            if (converter is not null)
            {
                Writer.WriteLine($"{anchorIndent}<!-- buildin:block:{block.Id} -->");
                var childCtx = new AnchoredRenderContext(Writer, Inline, _registry, IndentLevel, _anchorDepth + 1, _unknownIds);
                converter.Write(block, subtree.Children, childCtx);
            }
            else
            {
                Writer.WriteLine($"{anchorIndent}<!-- buildin:opaque:{block.Id} -->");
                _unknownIds.Add(block.Id);
                UnsupportedBlockHandler.Write(block, this);
            }
        }
    }
}
