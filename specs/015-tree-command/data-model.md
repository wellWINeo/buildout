# Data Model: Tree Command

This feature introduces a small set of immutable types in `Buildout.Core/PageTree/`.
None of them are persisted; all live for the duration of a single tree call.

## `TreeNode` (record)

The unit of the rendered hierarchy.

| Field | Type | Required | Description |
|---|---|---|---|
| `Name` | `string` | yes | Plain-text title of the page or database. `(untitled)` if the source title is empty or whitespace-only (FR-016). `(unavailable)` if a descendant read failed (FR-012a). |
| `Uri` | `string` | yes | The buildin.ai web URL the workspace exposes for this page/database, as returned by the existing buildin client (FR-004, FR-006). Empty string when the node is `(unavailable)` and no URL is known (FR-012a). |
| `Children` | `IReadOnlyList<TreeNode>` | yes | Ordered list of child `TreeNode`s in API-returned order (FR-014). Empty list (never null, never absent) on leaves (FR-006). |

**Construction rules**

- Built by `PageTreeService` while traversing.
- Marked `sealed record` with `init`-only setters; instances are immutable.
- No identity comparison helpers — equality is structural through the record contract.

## `TreeNodeKind` (internal enum)

Used only for service-internal branching and log tagging; **not** serialized,
**not** rendered.

```csharp
internal enum TreeNodeKind
{
    Page,
    Database,
    Unavailable,
}
```

## `TreeFormat` (public enum)

The user-facing format selector shared by CLI and MCP.

```csharp
public enum TreeFormat
{
    Ascii,
    Json,
}
```

- CLI `--format` flag accepts the string values `ascii` and `json` (lower-case
  only, parsed by Spectre); any other value yields exit code 2.
- MCP `format` parameter accepts the same string values; any other value
  yields `McpErrorCode.InvalidParams`.
- Default on both surfaces is `Ascii` (FR-003, FR-011).

## `TreeDepth` (static helper)

Single source of truth for the depth bounds.

| Member | Value | Description |
|---|---|---|
| `Min` | `1` | Smallest accepted depth (FR-008, FR-009). |
| `Max` | `7` | Largest accepted depth (FR-009). |
| `Default` | `3` | Used when no depth is supplied (FR-007). |
| `Validate(int depth)` | method | Throws `TreeDepthOutOfRangeException` if `depth < Min` or `depth > Max`; returns `depth` otherwise. |

## Exceptions

All thrown from `PageTreeService.BuildAsync`.

### `TreeDepthOutOfRangeException`

- **When**: depth parameter is `< 1` or `> 7`.
- **Message**: `"depth must be between 1 and 7 (inclusive); got {value}"`.
- **Inner**: none.
- **Mapping**: CLI exit code 2, MCP `InvalidParams`.

### `TreeRootNotFoundException`

- **When**: the root `GetPageAsync` or `GetDatabaseAsync` call returns 404, or
  both calls fail with a non-transport error (we attempt page first; if that
  returns 404 we attempt database, since the spec accepts either as the root).
- **Message**: `"page or database not found: {id}"`.
- **Inner**: the last `BuildinApiException` from the failed lookup.
- **Mapping**: CLI exit code 3, MCP `InvalidParams`.

### `TreeCycleDetectedException`

- **When**: a child's ID is already present in the visited-set during a single
  traversal.
- **Message**: `"cycle detected in page hierarchy at node {id}"`.
- **Inner**: none.
- **Mapping**: CLI exit code 7, MCP `InternalError`.

Other `BuildinApiException`s from descendant lookups are caught inside the
service and converted to an `(unavailable)` `TreeNode` rather than propagated.

## Service interface

### `IPageTreeService`

```csharp
public interface IPageTreeService
{
    Task<TreeNode> BuildAsync(string targetId, int depth, CancellationToken cancellationToken = default);
}
```

- `targetId`: a UUID string for a page or database (FR-001). The service tries
  `GetPageAsync` first; on 404 it falls back to `GetDatabaseAsync`. Other
  errors on the page lookup do not trigger the fallback (they propagate).
- `depth`: integer in `[TreeDepth.Min, TreeDepth.Max]`. Callers in CLI/MCP
  apply `TreeDepth.Default` when the user omitted the value.
- Returns the fully assembled tree.
- Throws the three exceptions above.

### Renderer interface

```csharp
public interface ITreeRenderer
{
    TreeFormat Format { get; }
    string Render(TreeNode root);
}
```

- One implementation per `TreeFormat`. Both are registered as
  `ITreeRenderer` and consumed via an `IReadOnlyDictionary<TreeFormat, ITreeRenderer>`
  built once at DI time (same pattern as
  `IReadOnlyDictionary<DatabaseViewStyle, IDatabaseViewStyle>`).

## State transitions

There are none — every type above is immutable, every call is one-shot.
The traversal itself is depth-first, left-to-right, and tracks two pieces of
local state:

- `visited`: `HashSet<string>` of node IDs seen so far in the current
  traversal (for cycle detection).
- `currentDepth`: `int` — increments on descent, decrements on return; the
  traversal stops descending when `currentDepth == depth`.

Neither piece of state escapes `BuildAsync`.
