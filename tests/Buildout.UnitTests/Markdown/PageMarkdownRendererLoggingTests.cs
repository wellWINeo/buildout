using System.Diagnostics.Metrics;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Caching;
using Buildout.Core.Markdown;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Conversion.Mentions;
using Buildout.Core.Markdown.Internal;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown;

[Collection("MetricsTests")]
public sealed class PageMarkdownRendererLoggingTests
{
    private static (PageMarkdownRenderer renderer, TestLogger<PageMarkdownRenderer> logger) CreateRenderer(IPageContentProvider contentProvider)
    {
        var blockConverters = new IBlockToMarkdownConverter[]
        {
            new ParagraphConverter(),
            new Heading1Converter(),
            new Heading2Converter(),
            new Heading3Converter(),
            new BulletedListItemConverter(),
            new NumberedListItemConverter(),
            new ToDoConverter(),
            new CodeConverter(),
            new QuoteConverter(),
            new DividerConverter()
        };
        var mentionConverters = new IMentionToMarkdownConverter[]
        {
            new PageMentionConverter(),
            new DatabaseMentionConverter(),
            new UserMentionConverter(),
            new DateMentionConverter()
        };
        var blockRegistry = new BlockToMarkdownRegistry(blockConverters);
        var mentionRegistry = new MentionToMarkdownRegistry(mentionConverters);
        var inlineRenderer = new InlineRenderer(mentionRegistry);
        var logger = new TestLogger<PageMarkdownRenderer>();
        return (new PageMarkdownRenderer(contentProvider, blockRegistry, inlineRenderer, logger), logger);
    }

    private static IPageContentProvider SetupContentProviderWithBlocks(string pageId, params Block[] blocks)
    {
        var contentProvider = Substitute.For<IPageContentProvider>();
        contentProvider.FetchAsync(pageId, Arg.Any<CancellationToken>())
            .Returns(new PageContent
            {
                Page = new Page { Id = pageId, Title = null },
                Blocks = blocks.Select(b => new BlockSubtree { Block = b, Children = [] }).ToList()
            });
        return contentProvider;
    }

    [Fact]
    public async Task RenderAsync_LogsOperationWithPageIdAndBlockCount_OnSuccess()
    {
        var block = new ParagraphBlock
        {
            Id = "b1",
            RichTextContent = [new RichText { Type = "text", Content = "Hello" }]
        };
        var contentProvider = SetupContentProviderWithBlocks("pg-1", block);
        var (sut, logger) = CreateRenderer(contentProvider);

        await sut.RenderAsync("pg-1");

        var completedEntry = logger.Entries.FirstOrDefault(e => e.Level == LogLevel.Information);
        Assert.NotNull(completedEntry);
        Assert.Contains("page_read", completedEntry.Message);
        Assert.Contains("pg-1", completedEntry.Message);
        Assert.Contains("block_count", completedEntry.Message);
    }

    [Fact]
    public async Task RenderAsync_RecordsBlocksProcessedTotal_OnSuccess()
    {
        var block1 = new ParagraphBlock
        {
            Id = "b1",
            RichTextContent = [new RichText { Type = "text", Content = "A" }]
        };
        var block2 = new ParagraphBlock
        {
            Id = "b2",
            RichTextContent = [new RichText { Type = "text", Content = "B" }]
        };
        var contentProvider = SetupContentProviderWithBlocks("pg-2", block1, block2);
        var (sut, _) = CreateRenderer(contentProvider);

        using var collector = new MeterCollector();
        await sut.RenderAsync("pg-2");

        Assert.Contains(collector.GetSnapshot(), r =>
            r.Name == "buildout.blocks.processed.total"
            && r.Value >= 2
            && r.Tags.Any(t => t.Key == "operation" && t.Value?.ToString() == "page_read"));
    }

    [Fact]
    public async Task RenderAsync_RecordsOperationsTotalSuccess_OnSuccess()
    {
        var contentProvider = SetupContentProviderWithBlocks("pg-3");
        var (sut, _) = CreateRenderer(contentProvider);

        using var collector = new MeterCollector();
        await sut.RenderAsync("pg-3");

        Assert.Contains(collector.GetSnapshot(), r =>
            r.Name == "buildout.operations.total"
            && r.Value > 0
            && r.Tags.Any(t => t.Key == "operation" && t.Value?.ToString() == "page_read")
            && r.Tags.Any(t => t.Key == "outcome" && t.Value?.ToString() == "success"));
    }

    [Fact]
    public async Task RenderAsync_RecordsOperationsTotalFailure_OnException()
    {
        var contentProvider = Substitute.For<IPageContentProvider>();
        contentProvider.FetchAsync("pg-err", Arg.Any<CancellationToken>())
            .Returns<Task<PageContent>>(_ => throw new InvalidOperationException("boom"));
        var (sut, _) = CreateRenderer(contentProvider);

        using var collector = new MeterCollector();
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RenderAsync("pg-err"));

        var recording = collector.GetSnapshot()
            .FirstOrDefault(r => r.Name == "buildout.operations.total"
                && r.Tags.Any(t => t.Key == "outcome" && t.Value?.ToString() == "failure"));
        Assert.True(recording.Value > 0);
        Assert.Contains(recording.Tags, t => t.Key == "operation" && t.Value?.ToString() == "page_read");
    }

    [Fact]
    public async Task RenderAsync_CountsNestedBlocks()
    {
        var parent = new BulletedListItemBlock
        {
            Id = "parent-1",
            HasChildren = true,
            RichTextContent = [new RichText { Type = "text", Content = "Parent" }]
        };
        var child = new ParagraphBlock
        {
            Id = "child-1",
            RichTextContent = [new RichText { Type = "text", Content = "Child" }]
        };

        var contentProvider = Substitute.For<IPageContentProvider>();
        contentProvider.FetchAsync("pg-nest", Arg.Any<CancellationToken>())
            .Returns(new PageContent
            {
                Page = new Page { Id = "pg-nest", Title = null },
                Blocks = [
                    new BlockSubtree { Block = parent, Children = [new BlockSubtree { Block = child, Children = [] }] }
                ]
            });

        var (sut, _) = CreateRenderer(contentProvider);

        using var collector = new MeterCollector();
        await sut.RenderAsync("pg-nest");

        Assert.Contains(collector.GetSnapshot(), r =>
            r.Name == "buildout.blocks.processed.total"
            && r.Value == 2
            && r.Tags.Any(t => t.Key == "operation" && t.Value?.ToString() == "page_read"));
    }

    [Fact]
    public async Task FetchChildrenAsync_LogsPaginationDebugEntries()
    {
        var contentProvider = Substitute.For<IPageContentProvider>();
        contentProvider.FetchAsync("pg-paginate", Arg.Any<CancellationToken>())
            .Returns(new PageContent
            {
                Page = new Page { Id = "pg-paginate", Title = null },
                Blocks = []
            });

        var (sut, logger) = CreateRenderer(contentProvider);

        await sut.RenderAsync("pg-paginate");

        var completedEntry = logger.Entries.FirstOrDefault(e => e.Level == LogLevel.Information);
        Assert.NotNull(completedEntry);
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class MeterCollector : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly object _lock = new();
        private readonly List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> _recordings = [];

        public MeterCollector()
        {
            _listener = new MeterListener();
            _listener.InstrumentPublished = (inst, listener) =>
            {
                if (inst.Meter.Name == "Buildout")
                    listener.EnableMeasurementEvents(inst);
            };
            _listener.SetMeasurementEventCallback<long>((inst, value, tags, _) =>
            {
                lock (_lock)
                    _recordings.Add((inst.Name, value, tags.ToArray()));
            });
            _listener.Start();
        }

        public (string Name, long Value, KeyValuePair<string, object?>[] Tags)[] GetSnapshot()
        {
            lock (_lock)
                return _recordings.ToArray();
        }

        public void Dispose() => _listener.Dispose();
    }
}