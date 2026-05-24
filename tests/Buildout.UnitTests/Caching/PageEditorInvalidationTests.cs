using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Caching;
using Buildout.Core.Markdown;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Conversion.Mentions;
using Buildout.Core.Markdown.Editing;
using Buildout.Core.Markdown.Editing.PatchOperations;
using Buildout.Core.Markdown.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Caching;

/// <summary>
/// Verifies that PageEditor.UpdateAsync invalidates the cache entry for the updated page
/// after a successful write, and does not invalidate on dry-run.
/// </summary>
[Collection("MetricsTests")]
public sealed class PageEditorInvalidationTests
{
    private const string PageId = "page-under-edit";

    private readonly IBuildinClient _client = Substitute.For<IBuildinClient>();
    private readonly IPageContentProvider _contentProvider = Substitute.For<IPageContentProvider>();
    private readonly IPageReadCache _cache = Substitute.For<IPageReadCache>();
    private readonly IPageMarkdownRenderer _renderer = Substitute.For<IPageMarkdownRenderer>();
    private readonly IOptions<LimitationsOptions> _options =
        Options.Create(new LimitationsOptions { LargeDeleteThreshold = 10 });
    private readonly ILogger<PageEditor> _logger = Substitute.For<ILogger<PageEditor>>();
    private readonly IMarkdownToBlocksParser _parser = Substitute.For<IMarkdownToBlocksParser>();
    private readonly PageEditor _sut;

    public PageEditorInvalidationTests()
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
        var registry = new BlockToMarkdownRegistry(blockConverters);

        var mentionConverters = new IMentionToMarkdownConverter[]
        {
            new PageMentionConverter(),
            new DatabaseMentionConverter(),
            new UserMentionConverter(),
            new DateMentionConverter()
        };
        var mentionRegistry = new MentionToMarkdownRegistry(mentionConverters);
        var inlineRenderer = new InlineRenderer(mentionRegistry);

        _sut = new PageEditor(
            _client, _contentProvider, _cache, _renderer, _options, _logger, registry, _parser, inlineRenderer);
    }

    private void SetupSingleParagraphPage(string blockId = "b1", string text = "Hello")
    {
        var block = new ParagraphBlock
        {
            Id = blockId,
            RichTextContent = [new RichText { Type = "text", Content = text }]
        };
        var subtree = new BlockSubtree { Block = block, Children = [] };
        _contentProvider
            .FetchAsync(PageId, Arg.Any<CancellationToken>())
            .Returns(new PageContent
            {
                Page = new Page { Id = PageId },
                Blocks = [subtree]
            });
    }

    [Fact]
    public async Task UpdateAsync_AfterSuccessfulWrite_InvalidatesPageCache()
    {
        SetupSingleParagraphPage("b1", "Hello");
        _client
            .UpdateBlockAsync(Arg.Any<string>(), Arg.Any<UpdateBlockRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ParagraphBlock { Id = "b1" });

        var snapshot = await _sut.FetchForEditAsync(PageId);
        await _sut.UpdateAsync(new UpdatePageInput
        {
            PageId = PageId,
            Revision = snapshot.Revision,
            Operations = [new ReplaceBlockOperation { Anchor = "b1", Markdown = "World" }]
        });

        _cache.Received(1).Invalidate(PageId);
    }

    [Fact]
    public async Task UpdateAsync_DryRun_DoesNotInvalidateCache()
    {
        SetupSingleParagraphPage("b1", "Hello");

        var snapshot = await _sut.FetchForEditAsync(PageId);
        await _sut.UpdateAsync(new UpdatePageInput
        {
            PageId = PageId,
            Revision = snapshot.Revision,
            Operations = [new ReplaceBlockOperation { Anchor = "b1", Markdown = "World" }],
            DryRun = true
        });

        _cache.DidNotReceive().Invalidate(Arg.Any<string>());
    }
}
