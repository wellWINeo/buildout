# Contract: IPageEditor

**Feature**: 008-markdown-page-update
**Date**: 2026-05-16

---

## Interface

```csharp
namespace Buildout.Core.Markdown.Editing;

public interface IPageEditor
{
    Task<AnchoredPageSnapshot> FetchForEditAsync(
        string pageId,
        CancellationToken cancellationToken = default);

    Task<ReconciliationSummary> UpdateAsync(
        UpdatePageInput input,
        CancellationToken cancellationToken = default);
}
```

---

## `FetchForEditAsync`

### Inputs

| Parameter | Constraint |
|-----------|------------|
| `pageId` | Non-empty string; validated before any network call |

### Behaviour

1. Call `IBuildinClient.GetPageAsync(pageId)` to fetch the page object (title).
2. Call `IBuildinClient.GetBlockChildrenAsync(pageId)` recursively (same pagination loop as
   `PageMarkdownRenderer.FetchChildrenAsync`) to build the block tree.
3. Render the anchored Markdown via `AnchoredMarkdownRenderer`:
   - Emit `<!-- buildin:root -->` as the very first line.
   - Emit `<!-- buildin:block:<id> -->` before each supported block at the correct nesting depth.
   - Emit `<!-- buildin:opaque:<id> -->` before each unsupported block (placeholder paragraph).
   - The body stripped of all anchor comments MUST equal `IPageMarkdownRenderer.RenderAsync`
     output for the same page state (SC-001).
4. Compute `Revision = RevisionTokenComputer.Compute(markdown)` — CRC32 of UTF-8 bytes, 8-char
   lowercase hex.
5. Collect `UnknownBlockIds` — all block IDs that produced an opaque anchor.
6. Wrap the call in `OperationRecorder.Start(logger, "page_read_editing")`.
7. Return `AnchoredPageSnapshot { Markdown, Revision, UnknownBlockIds }`.

### Errors

| Error | Condition |
|-------|-----------|
| `BuildinApiException` (404) | `pageId` does not exist — propagated; CLI maps to exit 3 |
| `BuildinApiException` (401/403) | Auth failure — propagated; CLI maps to exit 4 |
| `BuildinApiException` (transport) | Network failure — propagated; CLI maps to exit 5 |

---

## `UpdateAsync`

### Inputs

`UpdatePageInput` — see `data-model.md`. Validated before any buildin call:

| Rule | Error |
|------|-------|
| `PageId` non-empty | Validation exception |
| `Revision` non-empty | Validation exception |
| `Operations` non-empty (min 1) | Validation exception |

### Behaviour

1. Re-fetch the current block tree for `PageId` (same pipeline as `FetchForEditAsync`, steps
   1–3) to compute the current anchored Markdown and `currentRevision`.
2. **Revision check**: if `input.Revision != currentRevision`, throw
   `StaleRevisionException { current_revision = currentRevision }` — no further processing.
3. Parse the current anchored Markdown into `BlockSubtreeWithAnchor[]` via
   `AnchoredMarkdownParser`.
4. Apply `Operations` in order via `PatchApplicator.Apply(tree, operation)`. If any operation
   fails, the exception propagates immediately with the operation index; no write calls are
   issued (FR-008).
5. **Large-delete check**: count blocks in the original tree absent from the patched tree.
   If count > `PageEditorOptions.LargeDeleteThreshold` AND `input.AllowLargeDelete == false`,
   throw `LargeDeleteException`.
6. If `input.DryRun == true`: compute `NewRevision` (CRC32 of the serialised patched anchored
   Markdown), populate `ReconciliationSummary` with counts + `PostEditMarkdown`, and return —
   zero buildin write calls (FR-014).
7. Issue the minimal buildin write calls from `Reconciler.Diff(originalTree, patchedTree)`:
   - `UpdateBlockAsync` for blocks with changed payloads.
   - `DeleteBlockAsync` for blocks absent from the patched tree.
   - `AppendBlockChildrenAsync` for new blocks (no anchor in patched tree).
   If a write call fails mid-reconciliation, throw `PartialPatchException` with the partial
   revision token and the index of the failing operation (FR-024).
8. Re-fetch to compute `NewRevision` OR compute it from the known post-write state.
9. Wrap the entire call in `OperationRecorder.Start(logger, "page_update")`. On
   `PatchRejectedException`, call `recorder.Fail(ex.PatchErrorClass)`.

### Errors

| Exception | `PatchErrorClass` | Notes |
|-----------|-------------------|-------|
| `StaleRevisionException` | `patch.stale_revision` | Step 2; carries `current_revision` |
| `AmbiguousMatchException` | `patch.ambiguous_match` | Step 4; carries `old_str`, `match_count` |
| `NoMatchException` | `patch.no_match` | Step 4; carries `old_str` |
| `UnknownAnchorException` | `patch.unknown_anchor` | Step 4; carries `anchor` |
| `SectionAnchorNotHeadingException` | `patch.section_anchor_not_heading` | Step 4; carries `anchor` |
| `AnchorNotContainerException` | `patch.anchor_not_container` | Step 4; carries `anchor`, `block_type` |
| `ReorderNotSupportedException` | `patch.reorder_not_supported` | Step 5 (Reconciler); carries `anchor`, `old_position`, `new_position` |
| `UnsupportedBlockTouchedException` | `patch.unsupported_block_touched` | Step 4 or 5; carries `anchor` |
| `LargeDeleteException` | `patch.large_delete` | Step 5; carries `would_delete`, `threshold` |
| `PartialPatchException` | `patch.partial` | Step 7; carries `partial_revision`, `committed_op_index`, `buildin_error` |
| `BuildinApiException` (404/401/403/transport) | — | Propagated; mapped by CLI/MCP adapters |

---

## Exit-code taxonomy (CLI)

Reused from `GetCommand` and `CreateCommand`:

| Class | CLI exit code |
|-------|--------------|
| Validation | 2 |
| Page not found | 3 |
| Auth / authorisation | 4 |
| Transport | 5 |
| `patch-rejected` (any `patch.*` class) | 7 (new) |
| Unexpected / `patch.partial` | 6 |

Exit code 7 is new for this feature. The `patch.*` class name is written to stderr to enable
programmatic recovery (e.g., `if exit_code == 7 && stderr contains "patch.stale_revision"`).
