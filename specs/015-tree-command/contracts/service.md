# Contract: Core service & renderers

## `IPageTreeService`

```csharp
namespace Buildout.Core.PageTree;

public interface IPageTreeService
{
    Task<TreeNode> BuildAsync(
        string targetId,
        int depth,
        CancellationToken cancellationToken = default);
}
```

### Pre-conditions

- `targetId` is a non-empty string. Empty/whitespace input throws
  `ArgumentException`.
- `depth` is in `[TreeDepth.Min, TreeDepth.Max]`. Out-of-range values throw
  `TreeDepthOutOfRangeException` before any network call.

### Post-conditions

- Returns a `TreeNode` whose `Name` is the root's title (`(untitled)` if
  empty) and whose `Uri` is the root's buildin.ai web URL.
- The root's children, and their children recursively up to `depth` levels,
  are populated in the order the buildin API returned them (FR-014). The
  rendered hierarchy stops at `depth` levels below the root (i.e., `depth=1`
  yields the root plus its direct children only).
- Only nodes whose source block is `child_page` or `child_database`, or whose
  source is a `QueryDatabaseAsync` row, contribute to the tree. All other
  block types are skipped (FR-010).

### Failure modes

| Cause | Behavior |
|---|---|
| Root lookup returns 404 on both page and database | Throws `TreeRootNotFoundException`. |
| Root lookup fails with auth/transport/other | Propagates the underlying `BuildinApiException` unchanged. |
| Descendant page/database/child-enumeration call fails | The failing node is materialized as a `TreeNode` with `Name = "(unavailable)"`, `Uri = ""` (or the known URL if it was already fetched), `Children = []`. Traversal continues. The failure is logged at `Warning` with the node ID and the exception. |
| Repeated node ID encountered in the same traversal | Throws `TreeCycleDetectedException`. |
| Cancellation requested | Propagates `OperationCanceledException`. |

## `TreeNode`

```csharp
public sealed record TreeNode(
    string Name,
    string Uri,
    IReadOnlyList<TreeNode> Children);
```

- `Children` is never `null`; leaves use `Array.Empty<TreeNode>()` or an empty
  immutable list.

## `ITreeRenderer`

```csharp
public interface ITreeRenderer
{
    TreeFormat Format { get; }
    string Render(TreeNode root);
}
```

- `Render` is a pure function over its input. Two calls with the same
  `TreeNode` produce identical strings (SC-002, SC-004).
- ASCII renderer never emits a trailing newline beyond the final line
  terminator on the last node; JSON renderer emits a trailing newline for
  POSIX-tool friendliness.

## JSON shape

For every node:

```json
{
  "name": "string",
  "uri":  "string",
  "children": [ /* recursive */ ]
}
```

Property order is fixed: `name`, `uri`, `children`. `children` is always
present, including on leaves (FR-006). Strings are UTF-8 and contain raw
names — no HTML or markdown escaping is applied (FR-006). `null` is never
used; missing data is represented by empty strings (`uri`) or the
`(unavailable)` / `(untitled)` placeholders (`name`).

## ASCII shape

- Root line: `[Name](<URL>)`. No prefix.
- Each child line: a sequence of branch glyphs followed by `[Name](<URL>)`.
- Branch glyphs:
  - `├── ` — an intermediate child at this level.
  - `└── ` — the last child at this level.
  - `│   ` — a vertical bar plus three spaces; appears at columns where a
    branch continues at an ancestor level.
  - `    ` — four spaces; appears at columns where the ancestor's branch has
    closed.
- The renderer emits exactly one line per node, joined by `\n`. The final
  newline is omitted to keep CLI piping clean (matching `GetCommand`'s
  behavior).

## Renderer registration

`AddBuildoutCore` registers:

- `services.AddSingleton<ITreeRenderer, AsciiTreeRenderer>();`
- `services.AddSingleton<ITreeRenderer, JsonTreeRenderer>();`
- A keyed dictionary `IReadOnlyDictionary<TreeFormat, ITreeRenderer>`
  constructed from `GetServices<ITreeRenderer>()`, mirroring the
  `IDatabaseViewStyle` registration pattern in the same file.
- `services.AddSingleton<IPageTreeService, PageTreeService>();`
