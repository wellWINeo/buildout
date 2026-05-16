using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Conversion.Mentions;
using Buildout.Core.Markdown.Editing;
using Buildout.Core.Markdown.Internal;
using Buildout.Core.Markdown.Editing.Internal;
using Buildout.Core.Markdown.Editing.PatchOperations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Buildout.UnitTests.Markdown.Editing;

[Collection("MetricsTests")]
public sealed class PageEditorTests
{
    private readonly IBuildinClient _client;
    private readonly IPageMarkdownRenderer _renderer;
    private readonly IOptions<PageEditorOptions> _options;
    private readonly TestLogger _logger;
    private readonly BlockToMarkdownRegistry _registry;
    private readonly IMarkdownToBlocksParser _parser;
    private readonly IInlineRenderer _inlineRenderer;
    private readonly PageEditor _sut;

    public PageEditorTests()
    {
        _client = Substitute.For<IBuildinClient>();
        _renderer = Substitute.For<IPageMarkdownRenderer>();
        _options = Options.Create(new PageEditorOptions { LargeDeleteThreshold = 10 });
        _logger = new TestLogger();

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
        _registry = new BlockToMarkdownRegistry(blockConverters);

        var mentionConverters = new IMentionToMarkdownConverter[]
        {
            new PageMentionConverter(),
            new DatabaseMentionConverter(),
            new UserMentionConverter(),
            new DateMentionConverter()
        };
        var mentionRegistry = new MentionToMarkdownRegistry(mentionConverters);
        _inlineRenderer = new InlineRenderer(mentionRegistry);

        _parser = Substitute.For<IMarkdownToBlocksParser>();
        _sut = new PageEditor(_client, _renderer, _options, _logger, _registry, _parser, _inlineRenderer);
    }

    private async Task<(AnchoredPageSnapshot snapshot, string pageId)> SetupSingleParagraphPageAsync(
        string blockId = "p1", string text = "Hello")
    {
        var pageId = $"page-{blockId}";
        var block = new ParagraphBlock
        {
            Id = blockId,
            RichTextContent = [new RichText { Type = "text", Content = text }]
        };

        _client.GetPageAsync(pageId, Arg.Any<CancellationToken>())
            .Returns(new Page { Id = pageId });
        _client.GetBlockChildrenAsync(pageId, Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = [block], HasMore = false });

        var snapshot = await _sut.FetchForEditAsync(pageId);
        return (snapshot, pageId);
    }

    [Fact]
    public async Task UpdateAsync_OperationsAppliedInDeclaredOrder()
    {
        var (snapshot, pageId) = await SetupSingleParagraphPageAsync();

        var input = new UpdatePageInput
        {
            PageId = pageId,
            Revision = snapshot.Revision,
            Operations =
            [
                new SearchReplaceOperation { OldStr = "Hello", NewStr = "Hello World" },
                new SearchReplaceOperation { OldStr = "Hello World", NewStr = "Goodbye Universe" }
            ]
        };

        var result = await _sut.UpdateAsync(input);

        Assert.Contains("Goodbye Universe", result.PostEditMarkdown);
    }

    [Fact]
    public async Task UpdateAsync_FirstFailingOperation_AbortsWithZeroWrites()
    {
        var (snapshot, pageId) = await SetupSingleParagraphPageAsync();

        var input = new UpdatePageInput
        {
            PageId = pageId,
            Revision = snapshot.Revision,
            Operations =
            [
                new SearchReplaceOperation { OldStr = "Hello", NewStr = "World" },
                new ReplaceBlockOperation { Anchor = "nonexistent-anchor", Markdown = "text" },
                new SearchReplaceOperation { OldStr = "World", NewStr = "Universe" }
            ]
        };

        var ex = await Assert.ThrowsAnyAsync<PatchRejectedException>(() => _sut.UpdateAsync(input));

        Assert.IsType<UnknownAnchorException>(ex);
        Assert.Contains("nonexistent-anchor", ex.Message);

        await _client.DidNotReceive()
            .UpdateBlockAsync(Arg.Any<string>(), Arg.Any<UpdateBlockRequest>(), Arg.Any<CancellationToken>());
        await _client.DidNotReceive()
            .AppendBlockChildrenAsync(Arg.Any<string>(), Arg.Any<AppendBlockChildrenRequest>(), Arg.Any<CancellationToken>());
        await _client.DidNotReceive()
            .DeleteBlockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_PartialFailure_SurfacesPartialPatchException()
    {
        var (snapshot, pageId) = await SetupSingleParagraphPageAsync();

        _client.UpdateBlockAsync("p1", Arg.Any<UpdateBlockRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("API error"));

        var input = new UpdatePageInput
        {
            PageId = pageId,
            Revision = snapshot.Revision,
            Operations =
            [
                new ReplaceBlockOperation { Anchor = "p1", Markdown = "New content" }
            ]
        };

        var ex = await Assert.ThrowsAsync<PartialPatchException>(() => _sut.UpdateAsync(input));

        Assert.NotNull(ex.Details);
        Assert.True(ex.Details.ContainsKey("partial_revision"));
        Assert.True(ex.Details.ContainsKey("committed_op_index"));
    }

    [Fact]
    public async Task UpdateAsync_PatchRejected_CallsRecorderFailWithPatchErrorClass()
    {
        var (_, pageId) = await SetupSingleParagraphPageAsync("p1", "Hello");

        var wrongRevision = RevisionTokenComputer.Compute("different content entirely");
        var input = new UpdatePageInput
        {
            PageId = pageId,
            Revision = wrongRevision,
            Operations =
            [
                new SearchReplaceOperation { OldStr = "Hello", NewStr = "World" }
            ]
        };

        await Assert.ThrowsAsync<StaleRevisionException>(() => _sut.UpdateAsync(input));

        var errorEntry = _logger.Entries.LastOrDefault(e => e.Level == LogLevel.Error);
        Assert.NotNull(errorEntry);

        var patchErrorClass = typeof(StaleRevisionException)
            .GetProperty("PatchErrorClass")!
            .GetValue(Activator.CreateInstance(typeof(StaleRevisionException), "abc")!)!
            .ToString()!;
        Assert.Contains(patchErrorClass, errorEntry.Message);
    }

    [Fact]
    public async Task UpdateAsync_StaleRevision_ThrowsWithCurrentRevision()
    {
        var (_, pageId) = await SetupSingleParagraphPageAsync("p1", "Hello");

        var input = new UpdatePageInput
        {
            PageId = pageId,
            Revision = "stale-token",
            Operations =
            [
                new SearchReplaceOperation { OldStr = "Hello", NewStr = "World" }
            ]
        };

        var ex = await Assert.ThrowsAsync<StaleRevisionException>(() => _sut.UpdateAsync(input));

        Assert.NotNull(ex.Details);
        Assert.True(ex.Details.ContainsKey("current_revision"));
    }

    [Fact]
    public async Task UpdateAsync_MissingRevision_ThrowsBeforeFetch()
    {
        var input = new UpdatePageInput
        {
            PageId = "page-1",
            Revision = "",
            Operations =
            [
                new SearchReplaceOperation { OldStr = "Hello", NewStr = "World" }
            ]
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateAsync(input));

        await _client.DidNotReceive()
            .GetPageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _client.DidNotReceive()
            .GetBlockChildrenAsync(Arg.Any<string>(), Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>());
    }

    // Creates a page with a Heading1 block (id "h1") followed by (blockCount - 1) paragraph blocks.
    // Using ReplaceSectionOperation on "h1" will delete all blockCount original anchors.
    private async Task<(AnchoredPageSnapshot snapshot, string pageId)> SetupMultiBlockPageAsync(int blockCount)
    {
        var pageId = $"page-multi-{blockCount}";
        var heading = new Heading1Block
        {
            Id = "h1",
            RichTextContent = [new RichText { Type = "text", Content = "Section" }]
        };
        var paragraphs = Enumerable.Range(1, blockCount - 1).Select(i => (Block)new ParagraphBlock
        {
            Id = $"p{i:D2}",
            RichTextContent = [new RichText { Type = "text", Content = $"Paragraph {i}" }]
        });
        var blocks = new List<Block> { heading };
        blocks.AddRange(paragraphs);

        _client.GetPageAsync(pageId, Arg.Any<CancellationToken>())
            .Returns(new Page { Id = pageId });
        _client.GetBlockChildrenAsync(pageId, Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = blocks, HasMore = false });

        return (await _sut.FetchForEditAsync(pageId), pageId);
    }

    // T030(c): dry-run with stale revision still fails

    [Fact]
    public async Task UpdateAsync_DryRun_StaleRevision_StillFails()
    {
        var (_, pageId) = await SetupSingleParagraphPageAsync("p1", "Hello");

        var input = new UpdatePageInput
        {
            PageId = pageId,
            Revision = "stale-token",
            DryRun = true,
            Operations = [new SearchReplaceOperation { OldStr = "Hello", NewStr = "World" }]
        };

        await Assert.ThrowsAsync<StaleRevisionException>(() => _sut.UpdateAsync(input));

        await _client.DidNotReceive()
            .UpdateBlockAsync(Arg.Any<string>(), Arg.Any<UpdateBlockRequest>(), Arg.Any<CancellationToken>());
    }

    // T033: dry-run path tests

    [Fact]
    public async Task UpdateAsync_DryRun_IssuesNoWriteCalls()
    {
        var (snapshot, pageId) = await SetupSingleParagraphPageAsync("p1", "Hello");

        var input = new UpdatePageInput
        {
            PageId = pageId,
            Revision = snapshot.Revision,
            DryRun = true,
            Operations = [new SearchReplaceOperation { OldStr = "Hello", NewStr = "World" }]
        };

        await _sut.UpdateAsync(input);

        await _client.DidNotReceive()
            .UpdateBlockAsync(Arg.Any<string>(), Arg.Any<UpdateBlockRequest>(), Arg.Any<CancellationToken>());
        await _client.DidNotReceive()
            .AppendBlockChildrenAsync(Arg.Any<string>(), Arg.Any<AppendBlockChildrenRequest>(), Arg.Any<CancellationToken>());
        await _client.DidNotReceive()
            .DeleteBlockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_DryRun_CountsMatchCommittingRun()
    {
        var (snapshot, pageId) = await SetupSingleParagraphPageAsync("p1", "Hello");

        var input = new UpdatePageInput
        {
            PageId = pageId,
            Revision = snapshot.Revision,
            DryRun = true,
            Operations = [new SearchReplaceOperation { OldStr = "Hello", NewStr = "World" }]
        };

        var result = await _sut.UpdateAsync(input);

        Assert.Equal(1, result.UpdatedBlocks);
        Assert.Equal(0, result.NewBlocks);
        Assert.Equal(0, result.DeletedBlocks);
    }

    [Fact]
    public async Task UpdateAsync_DryRun_PostEditMarkdownReflectsEdit()
    {
        var (snapshot, pageId) = await SetupSingleParagraphPageAsync("p1", "Hello");

        var input = new UpdatePageInput
        {
            PageId = pageId,
            Revision = snapshot.Revision,
            DryRun = true,
            Operations = [new SearchReplaceOperation { OldStr = "Hello", NewStr = "World" }]
        };

        var result = await _sut.UpdateAsync(input);

        Assert.NotNull(result.PostEditMarkdown);
        Assert.Contains("World", result.PostEditMarkdown);
    }

    [Fact]
    public async Task UpdateAsync_DryRun_OperationFailure_StillThrows()
    {
        var (snapshot, pageId) = await SetupSingleParagraphPageAsync("p1", "Hello");

        var input = new UpdatePageInput
        {
            PageId = pageId,
            Revision = snapshot.Revision,
            DryRun = true,
            Operations = [new SearchReplaceOperation { OldStr = "NONEXISTENT", NewStr = "World" }]
        };

        await Assert.ThrowsAnyAsync<PatchRejectedException>(() => _sut.UpdateAsync(input));

        await _client.DidNotReceive()
            .UpdateBlockAsync(Arg.Any<string>(), Arg.Any<UpdateBlockRequest>(), Arg.Any<CancellationToken>());
    }

    // T036: large-delete guard tests
    //
    // SetupMultiBlockPageAsync(12) creates 1 Heading1 (id "h1") + 11 paragraphs (12 blocks total).
    // ReplaceSectionOperation on "h1" removes all 12 original anchors and adds new unanchored content,
    // so deletionCount = 12 which exceeds the threshold of 10.

    [Fact]
    public async Task UpdateAsync_LargeDelete_WithoutFlag_ThrowsLargeDeleteException()
    {
        var (snapshot, pageId) = await SetupMultiBlockPageAsync(12);

        var input = new UpdatePageInput
        {
            PageId = pageId,
            Revision = snapshot.Revision,
            AllowLargeDelete = false,
            Operations = [new ReplaceSectionOperation { Anchor = "h1", Markdown = "# Replacement heading" }]
        };

        var ex = await Assert.ThrowsAsync<LargeDeleteException>(() => _sut.UpdateAsync(input));

        Assert.NotNull(ex.Details);
        Assert.True(ex.Details.ContainsKey("would_delete"));
        Assert.True(ex.Details.ContainsKey("threshold"));
    }

    [Fact]
    public async Task UpdateAsync_LargeDelete_WithFlag_Commits()
    {
        var (snapshot, pageId) = await SetupMultiBlockPageAsync(12);

        _client.DeleteBlockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _client.AppendBlockChildrenAsync(Arg.Any<string>(), Arg.Any<AppendBlockChildrenRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AppendBlockChildrenResult { Results = [] });

        var input = new UpdatePageInput
        {
            PageId = pageId,
            Revision = snapshot.Revision,
            AllowLargeDelete = true,
            Operations = [new ReplaceSectionOperation { Anchor = "h1", Markdown = "# Replacement heading" }]
        };

        var result = await _sut.UpdateAsync(input);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdateAsync_DeletionBelowThreshold_IsNotBlocked()
    {
        var (snapshot, pageId) = await SetupSingleParagraphPageAsync("p1", "Hello");

        _client.UpdateBlockAsync(Arg.Any<string>(), Arg.Any<UpdateBlockRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ParagraphBlock { Id = "p1" });

        var input = new UpdatePageInput
        {
            PageId = pageId,
            Revision = snapshot.Revision,
            AllowLargeDelete = false,
            Operations = [new SearchReplaceOperation { OldStr = "Hello", NewStr = "World" }]
        };

        var result = await _sut.UpdateAsync(input);

        Assert.Equal(1, result.UpdatedBlocks);
    }

    [Fact]
    public async Task UpdateAsync_DryRun_LargeDelete_WithFlag_ReturnsCountsWithoutWrites()
    {
        var (snapshot, pageId) = await SetupMultiBlockPageAsync(12);

        var input = new UpdatePageInput
        {
            PageId = pageId,
            Revision = snapshot.Revision,
            DryRun = true,
            AllowLargeDelete = true,
            Operations = [new ReplaceSectionOperation { Anchor = "h1", Markdown = "# Replacement heading" }]
        };

        var result = await _sut.UpdateAsync(input);

        Assert.NotNull(result);
        await _client.DidNotReceive()
            .UpdateBlockAsync(Arg.Any<string>(), Arg.Any<UpdateBlockRequest>(), Arg.Any<CancellationToken>());
        await _client.DidNotReceive()
            .AppendBlockChildrenAsync(Arg.Any<string>(), Arg.Any<AppendBlockChildrenRequest>(), Arg.Any<CancellationToken>());
        await _client.DidNotReceive()
            .DeleteBlockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private sealed class TestLogger : ILogger<PageEditor>
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
}
