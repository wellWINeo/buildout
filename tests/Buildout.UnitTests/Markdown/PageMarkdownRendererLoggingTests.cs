using System.Diagnostics.Metrics;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
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
    private static (PageMarkdownRenderer renderer, TestLogger<PageMarkdownRenderer> logger) CreateRenderer(IBuildinClient client)
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
        return (new PageMarkdownRenderer(client, blockRegistry, inlineRenderer, logger), logger);
    }

    private static IBuildinClient SetupClientWithBlocks(string pageId, params Block[] blocks)
    {
        var client = Substitute.For<IBuildinClient>();
        client.GetPageAsync(pageId, Arg.Any<CancellationToken>())
            .Returns(new Page { Id = pageId, Title = null });
        client.GetBlockChildrenAsync(pageId, Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = blocks.ToList(), HasMore = false });
        return client;
    }

    [Fact]
    public async Task RenderAsync_LogsOperationWithPageIdAndBlockCount_OnSuccess()
    {
        var block = new ParagraphBlock
        {
            Id = "b1",
            RichTextContent = [new RichText { Type = "text", Content = "Hello" }]
        };
        var client = SetupClientWithBlocks("pg-1", block);
        var (sut, logger) = CreateRenderer(client);

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
        var client = SetupClientWithBlocks("pg-2", block1, block2);
        var (sut, _) = CreateRenderer(client);

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
        var client = SetupClientWithBlocks("pg-3");
        var (sut, _) = CreateRenderer(client);

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
        var client = Substitute.For<IBuildinClient>();
        client.GetPageAsync("pg-err", Arg.Any<CancellationToken>())
            .Returns<Task<Page>>(_ => throw new InvalidOperationException("boom"));
        var (sut, _) = CreateRenderer(client);

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

        var client = Substitute.For<IBuildinClient>();
        client.GetPageAsync("pg-nest", Arg.Any<CancellationToken>())
            .Returns(new Page { Id = "pg-nest", Title = null });
        client.GetBlockChildrenAsync("pg-nest", Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = [parent], HasMore = false });
        client.GetBlockChildrenAsync("parent-1", Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = [child], HasMore = false });

        var (sut, _) = CreateRenderer(client);

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

        const string pageId = "pg-paginate";
        var client = Substitute.For<IBuildinClient>();
        client.GetPageAsync(pageId, Arg.Any<CancellationToken>())
            .Returns(new Page { Id = pageId, Title = null });
        client.GetBlockChildrenAsync(pageId, Arg.Is<BlockChildrenQuery?>(q => q == null), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = [block1], HasMore = true, NextCursor = "cursor1" });
        client.GetBlockChildrenAsync(pageId, Arg.Is<BlockChildrenQuery?>(q => q != null && q.StartCursor == "cursor1"), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = [block2], HasMore = false });

        var (sut, logger) = CreateRenderer(client);

        await sut.RenderAsync(pageId);

        var debugEntries = logger.Entries
            .Where(e => e.Level == LogLevel.Debug && e.Message.Contains("pagination_page"))
            .ToList();
        Assert.True(debugEntries.Count >= 2);
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
