using System.Diagnostics.Metrics;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Caching;
using Buildout.Core.Markdown;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Conversion.Mentions;
using Buildout.Core.Markdown.Editing;
using Buildout.Core.Markdown.Editing.Internal;
using Buildout.Core.Markdown.Editing.PatchOperations;
using Buildout.Core.Markdown.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown.Editing;

[Collection("MetricsTests")]
public sealed class PageEditorLoggingTests : IDisposable
{
    private readonly IBuildinClient _client;
    private readonly PageEditor _sut;
    private readonly MeterCollector _collector;

    public PageEditorLoggingTests()
    {
        _client = Substitute.For<IBuildinClient>();

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
        var registry = new BlockToMarkdownRegistry(blockConverters);
        var mentionRegistry = new MentionToMarkdownRegistry(mentionConverters);
        var inlineRenderer = new InlineRenderer(mentionRegistry);
        var parser = Substitute.For<IMarkdownToBlocksParser>();
        var renderer = Substitute.For<IPageMarkdownRenderer>();
        var contentProvider = Substitute.For<IPageContentProvider>();
        var cache = Substitute.For<IPageReadCache>();
        var options = Options.Create(new LimitationsOptions { LargeDeleteThreshold = 10 });
        var logger = Substitute.For<ILogger<PageEditor>>();

        _sut = new PageEditor(_client, contentProvider, cache, renderer, options, logger, registry, parser, inlineRenderer);
        _collector = new MeterCollector();
    }

    public void Dispose() => _collector.Dispose();

    private void SetupPage(string pageId, params Block[] blocks)
    {
        _client.GetPageAsync(pageId, Arg.Any<CancellationToken>())
            .Returns(new Page { Id = pageId });
        _client.GetBlockChildrenAsync(pageId, Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = blocks.ToList(), HasMore = false });
    }

    [Fact]
    public async Task FetchForEditAsync_RecordsOperationTotal_ExactlyOnce()
    {
        SetupPage("page-1", new ParagraphBlock
        {
            Id = "p1",
            RichTextContent = [new RichText { Type = "text", Content = "Hello" }]
        });

        await _sut.FetchForEditAsync("page-1");

        var hits = _collector.GetSnapshot()
            .Where(r =>
                r.Name == "buildout.operations.total" &&
                r.Tags.Any(t => t.Key == "operation" && t.Value?.ToString() == "page_read_editing"))
            .ToArray();

        Assert.Single(hits);
    }

    [Fact]
    public async Task UpdateAsync_RecordsBlocksProcessedTotal_WithPageUpdateOperation()
    {
        SetupPage("page-2", new ParagraphBlock
        {
            Id = "p1",
            RichTextContent = [new RichText { Type = "text", Content = "Hello" }]
        });

        _client.UpdateBlockAsync(Arg.Any<string>(), Arg.Any<UpdateBlockRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ParagraphBlock { Id = "p1" });

        var snapshot = await _sut.FetchForEditAsync("page-2");

        await _sut.UpdateAsync(new UpdatePageInput
        {
            PageId = "page-2",
            Revision = snapshot.Revision,
            Operations = [new SearchReplaceOperation { OldStr = "Hello", NewStr = "World" }]
        });

        Assert.Contains(_collector.GetSnapshot(), r =>
            r.Name == "buildout.blocks.processed.total" &&
            r.Tags.Any(t => t.Key == "operation" && t.Value?.ToString() == "page_update"));
    }

    [Fact]
    public async Task UpdateAsync_DryRun_RecordsBlocksProcessedTotal_WithPageUpdateOperation()
    {
        SetupPage("page-3", new ParagraphBlock
        {
            Id = "p1",
            RichTextContent = [new RichText { Type = "text", Content = "Hello" }]
        });

        var snapshot = await _sut.FetchForEditAsync("page-3");

        await _sut.UpdateAsync(new UpdatePageInput
        {
            PageId = "page-3",
            Revision = snapshot.Revision,
            DryRun = true,
            Operations = [new SearchReplaceOperation { OldStr = "Hello", NewStr = "World" }]
        });

        Assert.Contains(_collector.GetSnapshot(), r =>
            r.Name == "buildout.blocks.processed.total" &&
            r.Tags.Any(t => t.Key == "operation" && t.Value?.ToString() == "page_update"));
    }

    [Fact]
    public async Task UpdateAsync_PreservedBlocks_ReflectsUnchangedBlockCount()
    {
        SetupPage("page-4",
            new ParagraphBlock { Id = "p1", RichTextContent = [new RichText { Type = "text", Content = "Change me" }] },
            new ParagraphBlock { Id = "p2", RichTextContent = [new RichText { Type = "text", Content = "Keep me" }] },
            new ParagraphBlock { Id = "p3", RichTextContent = [new RichText { Type = "text", Content = "Keep me too" }] });

        _client.UpdateBlockAsync(Arg.Any<string>(), Arg.Any<UpdateBlockRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ParagraphBlock { Id = "p1" });

        var snapshot = await _sut.FetchForEditAsync("page-4");

        var result = await _sut.UpdateAsync(new UpdatePageInput
        {
            PageId = "page-4",
            Revision = snapshot.Revision,
            Operations = [new SearchReplaceOperation { OldStr = "Change me", NewStr = "Changed" }]
        });

        Assert.Equal(2, result.PreservedBlocks);
    }

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
                return [.. _recordings];
        }

        public void Dispose() => _listener.Dispose();
    }
}
