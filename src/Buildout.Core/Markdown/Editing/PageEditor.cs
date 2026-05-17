using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Diagnostics;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Editing.Internal;
using Buildout.Core.Markdown.Internal;
using System.Diagnostics;
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
            // Validates page exists and is accessible (triggers 404/403 before the expensive tree fetch).
            _ = await _client.GetPageAsync(pageId, cancellationToken).ConfigureAwait(false);
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

    private Task<List<BlockSubtree>> FetchChildrenAsync(string parentId, CancellationToken ct)
        => BlockTreeFetcher.FetchAsync(_client, _registry, parentId, _logger, ct);

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

            // Validates page exists and is accessible (triggers 404/403 before the expensive tree fetch).
            _ = await _client.GetPageAsync(input.PageId, cancellationToken).ConfigureAwait(false);
            var roots = await FetchChildrenAsync(input.PageId, cancellationToken).ConfigureAwait(false);

            var (currentMarkdown, _) = _anchoredRenderer.Render(roots);
            var currentRevision = RevisionTokenComputer.Compute(currentMarkdown);

            if (input.Revision != currentRevision)
                throw new StaleRevisionException(currentRevision);

            var originalTree = AnchoredMarkdownParser.Parse(currentMarkdown).ToArray();

            var apiTypes = new Dictionary<string, string>();
            void CollectApiTypes(IReadOnlyList<BlockSubtree> nodes)
            {
                foreach (var n in nodes)
                {
                    if (n.Block.Id is not null)
                        apiTypes[n.Block.Id] = n.Block.Type;
                    CollectApiTypes(n.Children);
                }
            }
            CollectApiTypes(roots);

            BlockSubtreeWithAnchor PopulateType(BlockSubtreeWithAnchor node)
            {
                return node with
                {
                    OriginalApiBlockType = node.AnchorId is not null && apiTypes.TryGetValue(node.AnchorId, out var type) ? type : null,
                    Children = node.Children.Select(PopulateType).ToArray()
                };
            }
            originalTree = originalTree.Select(PopulateType).ToArray();

            var applicator = new PatchApplicator();
            var patchedTree = applicator.Apply(originalTree, input.Operations);

            var patchedMarkdown = AnchoredTreeSerializer.SerializeTree(patchedTree);

            CheckLargeDelete(originalTree, patchedTree, input);

            var newRevision = RevisionTokenComputer.Compute(patchedMarkdown);

            if (input.DryRun)
            {
                var dryWriteOps = Reconciler.Reconcile(originalTree, patchedTree);
                var dryUpdated = dryWriteOps.OfType<WriteOp.Update>().Count();
                var dryDeleted = dryWriteOps.OfType<WriteOp.Delete>().Count();
                var dryNew = dryWriteOps.OfType<WriteOp.Append>().Count();
                var dryPreserved = CollectBlockAnchorIds(originalTree).Count - dryUpdated - dryDeleted;

                BuildoutMeter.BlocksProcessedTotal.Add(
                    dryPreserved + dryUpdated + dryDeleted + dryNew,
                    new TagList { { "operation", "page_update" } });

                recorder.SetTag("page_id", input.PageId);
                recorder.SetTag("dry_run", true);
                recorder.Succeed();

                return new ReconciliationSummary
                {
                    PreservedBlocks = dryPreserved,
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

            var preservedBlocks = CollectBlockAnchorIds(originalTree).Count - summary.UpdatedBlocks - summary.DeletedBlocks;

            BuildoutMeter.BlocksProcessedTotal.Add(
                preservedBlocks + summary.UpdatedBlocks + summary.DeletedBlocks + summary.NewBlocks,
                new TagList { { "operation", "page_update" } });

            recorder.SetTag("page_id", input.PageId);
            recorder.SetTag("updated_blocks", summary.UpdatedBlocks);
            recorder.SetTag("deleted_blocks", summary.DeletedBlocks);
            recorder.SetTag("new_blocks", summary.NewBlocks);
            recorder.Succeed();

            return summary with { NewRevision = newRevision, PostEditMarkdown = null, PreservedBlocks = preservedBlocks };
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
                            ToUpdateBlockRequest(u.Block.Block, u.OriginalApiBlockType),
                            cancellationToken).ConfigureAwait(false);
                        updatedBlocks++;
                        break;
                    case WriteOp.Delete d:
                        await _client.DeleteBlockAsync(d.AnchorId, cancellationToken).ConfigureAwait(false);
                        deletedBlocks++;
                        break;
                    case WriteOp.Append a:
                        var parentId = a.ParentId == "root" ? pageId : a.ParentId;
                        await AppendSubtreeAsync(parentId, a.Block, cancellationToken).ConfigureAwait(false);
                        newBlocks++;
                        break;
                }
            }
            catch (Exception ex) when (ex is not PatchRejectedException)
            {
                throw new PartialPatchException("", i, ex);
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

    // heading_1 renders as "## " via Heading1Converter, so AnchoredMarkdownParser produces Heading2Block.
    // Only restore "heading_1" when the updated block is still a Heading2Block (content-only edit);
    // if replace_block changed it to a different type, honour the new type.
    private static UpdateBlockRequest ToUpdateBlockRequest(Block block, string? originalApiBlockType)
    {
        return block switch
        {
            ParagraphBlock p => new UpdateBlockRequest { Type = p.Type, RichTextContent = p.RichTextContent },
            Heading1Block h => new UpdateBlockRequest { Type = h.Type, RichTextContent = h.RichTextContent },
            Heading2Block h => new UpdateBlockRequest { Type = originalApiBlockType == "heading_1" ? "heading_1" : h.Type, RichTextContent = h.RichTextContent },
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
    }

    private async Task AppendSubtreeAsync(string parentId, BlockSubtreeWrite subtree, CancellationToken ct)
    {
        var result = await _client.AppendBlockChildrenAsync(
            parentId,
            new AppendBlockChildrenRequest { Children = [subtree.Block] },
            ct).ConfigureAwait(false);

        if (subtree.Children.Count > 0 && result.Results.Count > 0)
        {
            var newBlockId = result.Results[0].Id;
            foreach (var child in subtree.Children)
                await AppendSubtreeAsync(newBlockId, child, ct);
        }
    }
}
