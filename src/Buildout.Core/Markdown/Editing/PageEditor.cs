using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Diagnostics;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Editing.Internal;
using Buildout.Core.Markdown.Internal;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Buildout.Core.Markdown.Editing;

[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Dynamic operation names prevent compile-time LoggerMessage definitions")]
public sealed class PageEditor : IPageEditor
{
    private readonly IBuildinClient _client;
    private readonly IPageMarkdownRenderer _renderer;
    private readonly IOptions<PageEditorOptions> _options;
    private readonly ILogger<PageEditor> _logger;
    private readonly BlockToMarkdownRegistry _registry;
    private readonly IMarkdownToBlocksParser _parser;
    private readonly IInlineRenderer _inlineRenderer;
    private readonly AnchoredMarkdownRenderer _anchoredRenderer;

    public PageEditor(
        IBuildinClient client,
        IPageMarkdownRenderer renderer,
        IOptions<PageEditorOptions> options,
        ILogger<PageEditor> logger,
        BlockToMarkdownRegistry registry,
        IMarkdownToBlocksParser parser,
        IInlineRenderer inlineRenderer)
    {
        _client = client;
        _renderer = renderer;
        _options = options;
        _logger = logger;
        _registry = registry;
        _parser = parser;
        _inlineRenderer = inlineRenderer;
        _anchoredRenderer = new AnchoredMarkdownRenderer(inlineRenderer, registry);
    }

    public async Task<AnchoredPageSnapshot> FetchForEditAsync(
        string pageId,
        CancellationToken cancellationToken = default)
    {
        using var recorder = OperationRecorder.Start(_logger, "page_read_editing");
        try
        {
            var page = await _client.GetPageAsync(pageId, cancellationToken).ConfigureAwait(false);
            var roots = await FetchChildrenAsync(pageId, cancellationToken).ConfigureAwait(false);

            var (markdown, unknownBlockIds) = _anchoredRenderer.Render(roots);
            var revision = RevisionTokenComputer.Compute(markdown);

            recorder.SetTag("page_id", pageId);
            recorder.SetTag("block_count", CountBlocks(roots));
            recorder.Succeed();

            return new AnchoredPageSnapshot
            {
                Markdown = markdown,
                Revision = revision,
                UnknownBlockIds = unknownBlockIds
            };
        }
        catch
        {
            recorder.Fail("unknown");
            throw;
        }
    }

    private async Task<List<BlockSubtree>> FetchChildrenAsync(string parentId, CancellationToken ct)
    {
        var result = new List<BlockSubtree>();
        string? cursor = null;
        var pageNumber = 1;

        do
        {
            var query = cursor is not null
                ? new BlockChildrenQuery { StartCursor = cursor }
                : null;

            var page = await _client
                .GetBlockChildrenAsync(parentId, query, ct)
                .ConfigureAwait(false);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("FetchChildren pagination_page={PageNumber} pagination_items={ItemsCount}", pageNumber, page.Results.Count);

            foreach (var block in page.Results)
            {
                List<BlockSubtree>? children = null;

                if (block.HasChildren)
                {
                    var converter = _registry.Resolve(block);
                    if (converter is { RecurseChildren: true })
                        children = await FetchChildrenAsync(block.Id, ct).ConfigureAwait(false);
                }

                result.Add(new BlockSubtree
                {
                    Block = block,
                    Children = children ?? []
                });
            }

            cursor = page.HasMore ? page.NextCursor : null;
            pageNumber++;
        }
        while (cursor is not null);

        return result;
    }

    private static int CountBlocks(IReadOnlyList<BlockSubtree> subtrees)
    {
        var count = 0;
        foreach (var subtree in subtrees)
        {
            count++;
            count += CountBlocks(subtree.Children);
        }
        return count;
    }

    public async Task<ReconciliationSummary> UpdateAsync(
        UpdatePageInput input,
        CancellationToken cancellationToken = default)
    {
        using var recorder = OperationRecorder.Start(_logger, "page_update");
        try
        {
            ValidateInput(input);

            var page = await _client.GetPageAsync(input.PageId, cancellationToken).ConfigureAwait(false);
            var roots = await FetchChildrenAsync(input.PageId, cancellationToken).ConfigureAwait(false);

            var (currentMarkdown, _) = _anchoredRenderer.Render(roots);
            var currentRevision = RevisionTokenComputer.Compute(currentMarkdown);

            if (input.Revision != currentRevision)
                throw new StaleRevisionException(currentRevision);

            var originalTree = AnchoredMarkdownParser.Parse(currentMarkdown).ToArray();
            var applicator = new PatchApplicator();
            var patchedTree = applicator.Apply(originalTree, input.Operations);

            var patchedMarkdown = SerializeAnchoredTree(patchedTree);

            CheckLargeDelete(originalTree, patchedTree, input);

            var newRevision = RevisionTokenComputer.Compute(patchedMarkdown);

            if (input.DryRun)
            {
                var dryWriteOps = Reconciler.Reconcile(originalTree, patchedTree);
                var dryUpdated = dryWriteOps.OfType<WriteOp.Update>().Count();
                var dryDeleted = dryWriteOps.OfType<WriteOp.Delete>().Count();
                var dryNew = dryWriteOps.OfType<WriteOp.Append>().Count();

                recorder.SetTag("page_id", input.PageId);
                recorder.SetTag("dry_run", true);
                recorder.Succeed();

                return new ReconciliationSummary
                {
                    PreservedBlocks = 0,
                    UpdatedBlocks = dryUpdated,
                    NewBlocks = dryNew,
                    DeletedBlocks = dryDeleted,
                    AmbiguousMatches = 0,
                    NewRevision = newRevision,
                    PostEditMarkdown = patchedMarkdown
                };
            }

            var writeOps = Reconciler.Reconcile(originalTree, patchedTree);

            var summary = await ExecuteWriteOpsAsync(writeOps, input.PageId, cancellationToken).ConfigureAwait(false);

            recorder.SetTag("page_id", input.PageId);
            recorder.SetTag("updated_blocks", summary.UpdatedBlocks);
            recorder.SetTag("deleted_blocks", summary.DeletedBlocks);
            recorder.SetTag("new_blocks", summary.NewBlocks);
            recorder.Succeed();

            return summary with { NewRevision = newRevision, PostEditMarkdown = patchedMarkdown };
        }
        catch (PatchRejectedException ex)
        {
            recorder.Fail(ex.PatchErrorClass);
            throw;
        }
        catch
        {
            recorder.Fail("unknown");
            throw;
        }
    }

    private static void ValidateInput(UpdatePageInput input)
    {
        ArgumentException.ThrowIfNullOrEmpty(input.PageId);
        ArgumentException.ThrowIfNullOrEmpty(input.Revision);
        if (input.Operations is null or { Count: 0 })
            throw new ArgumentException("Operations must not be empty.", nameof(input));
    }

    private void CheckLargeDelete(
        IReadOnlyList<BlockSubtreeWithAnchor> originalTree,
        IReadOnlyList<BlockSubtreeWithAnchor> patchedTree,
        UpdatePageInput input)
    {
        var originalAnchors = CollectBlockAnchorIds(originalTree);
        var patchedAnchors = CollectBlockAnchorIds(patchedTree);
        var deletionCount = originalAnchors.Except(patchedAnchors).Count();
        if (deletionCount > _options.Value.LargeDeleteThreshold && !input.AllowLargeDelete)
            throw new LargeDeleteException(deletionCount, _options.Value.LargeDeleteThreshold);
    }

    private static HashSet<string> CollectBlockAnchorIds(IReadOnlyList<BlockSubtreeWithAnchor> nodes)
    {
        var result = new HashSet<string>();
        foreach (var node in nodes)
        {
            if (node.AnchorId is not null && node.AnchorKind == AnchorKind.Block)
                result.Add(node.AnchorId);
            if (node.Children.Count > 0)
                foreach (var id in CollectBlockAnchorIds(node.Children))
                    result.Add(id);
        }
        return result;
    }

    private async Task<ReconciliationSummary> ExecuteWriteOpsAsync(
        IReadOnlyList<WriteOp> writeOps,
        string pageId,
        CancellationToken cancellationToken)
    {
        var updatedBlocks = 0;
        var newBlocks = 0;
        var deletedBlocks = 0;
        var preservedBlocks = 0;

        for (var i = 0; i < writeOps.Count; i++)
        {
            var op = writeOps[i];
            try
            {
                switch (op)
                {
                    case WriteOp.Update u:
                        await _client.UpdateBlockAsync(
                            u.AnchorId,
                            ToUpdateBlockRequest(u.Block.Block),
                            cancellationToken).ConfigureAwait(false);
                        updatedBlocks++;
                        break;
                    case WriteOp.Delete d:
                        await _client.DeleteBlockAsync(d.AnchorId, cancellationToken).ConfigureAwait(false);
                        deletedBlocks++;
                        break;
                    case WriteOp.Append a:
                        await _client.AppendBlockChildrenAsync(
                            a.ParentId,
                            new AppendBlockChildrenRequest
                            {
                                Children = FlattenSubtreeWrite(a.Block)
                            },
                            cancellationToken).ConfigureAwait(false);
                        newBlocks++;
                        break;
                }
            }
            catch (Exception ex) when (ex is not PatchRejectedException)
            {
                throw new PartialPatchException("partial", i, ex);
            }
        }

        return new ReconciliationSummary
        {
            PreservedBlocks = preservedBlocks,
            UpdatedBlocks = updatedBlocks,
            NewBlocks = newBlocks,
            DeletedBlocks = deletedBlocks,
            AmbiguousMatches = 0,
            NewRevision = ""
        };
    }

    private static UpdateBlockRequest ToUpdateBlockRequest(Block block) => block switch
    {
        ParagraphBlock p => new UpdateBlockRequest { Type = p.Type, RichTextContent = p.RichTextContent },
        Heading1Block h => new UpdateBlockRequest { Type = h.Type, RichTextContent = h.RichTextContent },
        Heading2Block h => new UpdateBlockRequest { Type = h.Type, RichTextContent = h.RichTextContent },
        Heading3Block h => new UpdateBlockRequest { Type = h.Type, RichTextContent = h.RichTextContent },
        BulletedListItemBlock b => new UpdateBlockRequest { Type = b.Type, RichTextContent = b.RichTextContent },
        NumberedListItemBlock n => new UpdateBlockRequest { Type = n.Type, RichTextContent = n.RichTextContent },
        ToDoBlock t => new UpdateBlockRequest { Type = t.Type, RichTextContent = t.RichTextContent, Checked = t.Checked },
        ToggleBlock t => new UpdateBlockRequest { Type = t.Type, RichTextContent = t.RichTextContent },
        CodeBlock c => new UpdateBlockRequest { Type = c.Type, RichTextContent = c.RichTextContent, Language = c.Language },
        QuoteBlock q => new UpdateBlockRequest { Type = q.Type, RichTextContent = q.RichTextContent },
        ImageBlock i => new UpdateBlockRequest { Type = i.Type, Url = i.Url },
        EmbedBlock e => new UpdateBlockRequest { Type = e.Type, Url = e.Url },
        _ => new UpdateBlockRequest { Type = block.Type }
    };

    private static List<Block> FlattenSubtreeWrite(BlockSubtreeWrite write)
    {
        var result = new List<Block> { write.Block };
        foreach (var child in write.Children)
            result.AddRange(FlattenSubtreeWrite(child));
        return result;
    }

    private static string SerializeAnchoredTree(IReadOnlyList<BlockSubtreeWithAnchor> nodes)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var node in nodes)
            SerializeAnchoredNode(sb, node);
        return sb.ToString();
    }

    private static void SerializeAnchoredNode(System.Text.StringBuilder sb, BlockSubtreeWithAnchor node)
    {
        if (node.AnchorKind == AnchorKind.Root)
        {
            sb.AppendLine("<!-- buildin:root -->");
            sb.AppendLine();
            foreach (var child in node.Children)
                SerializeAnchoredNode(sb, child);
            return;
        }

        if (node.AnchorKind == AnchorKind.Opaque && node.AnchorId is not null)
            sb.Append("<!-- buildin:opaque:").Append(node.AnchorId).AppendLine(" -->");
        else if (node.AnchorId is not null)
            sb.Append("<!-- buildin:block:").Append(node.AnchorId).AppendLine(" -->");

        var block = node.Block?.Block;
        switch (block)
        {
            case Heading1Block h:
                sb.Append("# ").AppendLine(RenderInlineText(h.RichTextContent));
                break;
            case Heading2Block h:
                sb.Append("## ").AppendLine(RenderInlineText(h.RichTextContent));
                break;
            case Heading3Block h:
                sb.Append("### ").AppendLine(RenderInlineText(h.RichTextContent));
                break;
            case ParagraphBlock p:
                sb.AppendLine(RenderInlineText(p.RichTextContent));
                break;
            case CodeBlock c:
                sb.Append("```").AppendLine(c.Language ?? string.Empty);
                sb.AppendLine(RenderInlineText(c.RichTextContent));
                sb.AppendLine("```");
                break;
            case QuoteBlock q:
                sb.Append("> ").AppendLine(RenderInlineText(q.RichTextContent));
                break;
            case DividerBlock:
                sb.AppendLine("---");
                break;
        }

        sb.AppendLine();

        foreach (var child in node.Children)
            SerializeAnchoredNode(sb, child);
    }

    private static string RenderInlineText(IReadOnlyList<RichText>? items)
    {
        if (items is null or { Count: 0 })
            return string.Empty;
        var sb = new System.Text.StringBuilder();
        foreach (var item in items)
            sb.Append(item.Content);
        return sb.ToString();
    }
}
