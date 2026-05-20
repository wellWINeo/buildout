# Data Model — Page Delete and Restore

In-memory shapes the lifecycle service operates over. Every shape lives in
`Buildout.Core` (Principle I: presentation layers do not parse these types).

## `PageLifecycleOutcome` (new)

```csharp
namespace Buildout.Core.PageLifecycle;

using Buildout.Core.Markdown.Authoring; // for FailureClass

public sealed record PageLifecycleOutcome
{
    /// <summary>The page that was acted on (echoes the input page_id).</summary>
    public required string PageId { get; init; }

    /// <summary>The final archived state of the page after the operation.
    /// On success: the post-operation value (true after delete, false after restore).
    /// On no-op short-circuit: the already-current value (unchanged).
    /// On failure: <see langword="null"/> — the final state is unknown.</summary>
    public bool? Archived { get; init; }

    /// <summary>True iff the operation actually issued a PATCH and changed the page's
    /// archived state. False for idempotent no-op short-circuits (page was already in
    /// the target state).</summary>
    public required bool Changed { get; init; }

    /// <summary>Set iff the operation failed. Reuses
    /// <see cref="Buildout.Core.Markdown.Authoring.FailureClass"/> to keep the error
    /// taxonomy unified across all page-affecting operations.</summary>
    public FailureClass? FailureClass { get; init; }

    /// <summary>The underlying exception when <see cref="FailureClass"/> is set.
    /// <see langword="null"/> on success.</summary>
    public Exception? UnderlyingException { get; init; }
}
```

**State invariants**:

- `FailureClass is null` ⇔ the operation succeeded (state change or no-op short-circuit).
- `FailureClass is null` ⇒ `Archived is not null`.
- `Changed is true` ⇒ exactly one `IBuildinClient.UpdatePageAsync` call was issued.
- `Changed is false` ∧ `FailureClass is null` ⇒ zero `UpdatePageAsync` calls were issued
  (no-op short-circuit). Exactly one `IBuildinClient.GetPageAsync` call was issued.
- `FailureClass is not null` ⇒ `UnderlyingException is not null`.

## Reused: `Buildout.Core.Markdown.Authoring.FailureClass` (no changes)

Defined in `Buildout.Core/Markdown/Authoring/CreatePageOutcome.cs`:

```csharp
public enum FailureClass
{
    Validation,
    NotFound,
    Auth,
    Transport,
    Unexpected,
    Partial
}
```

For page lifecycle: only `NotFound`, `Auth`, `Transport`, `Unexpected` can occur.
`Validation` is unreachable (the only input is a page-ID string; an empty string is
rejected by the CLI/MCP parameter binding before reaching the core service, surfaced as
an exit code 2 / `McpErrorCode.InvalidParams`). `Partial` is unreachable (the
operation is a single atomic PATCH at the server).

## Reused: `Buildout.Core.Buildin.Models.Page` (no changes)

The lifecycle service reads from and returns through this existing record. Relevant
fields:

- `Id: string` — the page identifier.
- `Archived: bool` — the flag this feature flips.
- everything else (properties, parent, url, icon, cover, title, timestamps) — read-only
  pass-through; the PATCH body never touches them.

## Reused: `Buildout.Core.Buildin.Models.UpdatePageRequest` (no changes)

The lifecycle service writes through this existing record. **The implementation populates
only `Archived` on every call.** All other fields (`Properties`, `Icon`, `Cover`) are
left at their default `null` value, which causes the Kiota serializer to omit them from
the JSON body, which causes buildin's PATCH endpoint to leave them untouched server-side
(see research.md R1).

## State Transitions

```text
                ┌─────────────────────────────────────┐
                │           Active page               │
                │           (Archived: false)         │
                └──────────────┬──────────────────────┘
                               │
                ┌──────────────┴──────────────┐
                │                             │
   DeleteAsync (Changed=true)        RestoreAsync (Changed=false, no-op)
                │                             │
                ▼                             │
                ┌─────────────────────────────────────┐
                │          Archived page              │
                │          (Archived: true)           │
                └──────────────┬──────────────────────┘
                               │
                ┌──────────────┴──────────────┐
                │                             │
   RestoreAsync (Changed=true)        DeleteAsync (Changed=false, no-op)
                │                             │
                ▼                             │
                back to Active
```

No other transitions exist. The lifecycle service does not delete blocks, modify
properties, or affect parent relationships.
