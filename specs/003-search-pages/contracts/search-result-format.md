# Contract — Search result body format

The line-oriented body produced by `ISearchResultFormatter.Format(matches)`
and consumed byte-for-byte by both presentation surfaces (CLI plain mode
and MCP tool result content). This is the canonical wire form between
core and presentation; **it is the same string in both surfaces** per
spec FR-014.

## Public surface

Located at `src/Buildout.Core/Search/ISearchResultFormatter.cs`.

```text
namespace Buildout.Core.Search;

public interface ISearchResultFormatter
{
    string Format(IReadOnlyList<SearchMatch> matches);
}
```

The default implementation is `SearchResultFormatter`, registered as a
singleton via `AddBuildoutCore`. It has zero dependencies and zero
state.

## Grammar

```text
body         := match-line*
match-line   := PAGE_ID TAB OBJECT_TYPE TAB TITLE LF
PAGE_ID      := 8 lowercase-hex "-" 4 "-" 4 "-" 4 "-" 12
OBJECT_TYPE  := "page" | "database"
TITLE        := non-empty sequence of UTF-8 characters with no LF and no TAB
TAB          := U+0009
LF           := U+000A
```

Empty body — i.e. exactly the empty string `""` — iff zero matches.

## Field rules

### `PAGE_ID`

- Lowercase, hyphenated UUID. Format-equivalent to
  `Guid.ToString("D").ToLowerInvariant()`.
- Sourced from `SearchMatch.PageId`.
- Always present and well-formed: `SearchService` constructs
  `SearchMatch.PageId` from `Page.Id` via `Guid.Parse(...).ToString("D")`,
  so any malformed input from buildin would surface earlier as a
  `BuildinApiException`.

### `OBJECT_TYPE`

- Exactly the literal string `page` or `database`. No other values in
  v1.
- Sourced from `SearchMatch.ObjectType` via
  `objectType.ToString().ToLowerInvariant()`.

### `TITLE`

- Sourced from `SearchMatch.DisplayTitle` (already plain-text by
  construction in `TitleRenderer`).
- Empty / whitespace titles surface as the literal placeholder
  `(untitled)` (parens included). `TitleRenderer` enforces this.
- Tab characters in titles are replaced with single space characters
  by `TitleRenderer` before they reach the formatter. The formatter
  trusts that invariant and does no further escaping.
- Newlines in titles are not possible — buildin titles are inherently
  single-line. The formatter does not check for them; if buildin ever
  starts emitting newlines in titles, the formatter's output would
  break the per-line invariant, and a regression test would fail
  (`TitleRendererTests.NewlineInTitleIsReplaced` is added defensively).

### Line termination

- Each match-line ends with exactly one `LF` (`\n`, U+000A). No
  `CRLF`. No trailing `LF` after the last line for non-empty bodies?
  — *yes, there is one trailing `LF`*: every line, including the
  last, terminates with `LF`. This means `body.EndsWith("\n")` for
  non-empty bodies, which simplifies the `xargs buildout get`
  pipeline (each input line ends naturally; no extra splitting
  required).
- Empty body is exactly `""` (zero bytes). It is NOT `"\n"`.

## Stability

For a fixed `IReadOnlyList<SearchMatch>` input, the formatter
produces a byte-identical output on every call. No timestamps, no
randomness, no environment-dependent values. Verified by
`SearchResultFormatterTests.OutputIsByteStable`.

## Consumers

### CLI plain mode

`SearchCommand` writes the body to `Console.Out` via
`Console.Out.WriteAsync(body)`. No transformation. The byte stream the
shell sees is exactly the formatter's body.

### CLI styled mode

`SearchResultStyledRenderer.Render(body, console)`:

1. Splits the body on `\n` (excluding the trailing empty token from
   the final terminator).
2. Splits each line on `\t` (expects exactly three tokens; mismatched
   token count is an internal bug — throws `InvalidOperationException`).
3. Emits a `Spectre.Console.Table` with three columns (`ID`, `Type`,
   `Title`).
4. For an empty body, emits a single styled line `[dim]No matches.[/]`.

The styled output is presentation-only and is NOT byte-comparable to
the plain body — it adds ANSI escape sequences and Spectre's table
chrome.

### MCP tool result

`SearchToolHandler` returns the body as the tool method's `string`
return value. The SDK wraps it in a single
`TextContentBlock(Text = body)` inside the resulting
`CallToolResponse`. The content block's `MimeType` is left at the
SDK default (`text/plain` for tool text content); the LLM consumes
the body as plain text.

## Test obligations

Unit tests live at
`tests/Buildout.UnitTests/Search/SearchResultFormatterTests.cs`:

| Test | Asserts |
|---|---|
| `Empty_ReturnsEmptyString` | `Format([]) == ""` |
| `SingleMatch_ProducesExpectedLine` | One match → exactly one line of the documented shape ending in `\n` |
| `MultipleMatches_PreserveOrder` | Three matches in input order produce three lines in input order |
| `UntitledMatch_UsesPlaceholder` | A match with `DisplayTitle == "(untitled)"` (post-`TitleRenderer`) renders the placeholder verbatim |
| `Database_IsLowerCase` | `ObjectType.Database` → `"database"` in column 2 |
| `Page_IsLowerCase` | `ObjectType.Page` → `"page"` in column 2 |
| `OutputIsByteStable` | `Format(input)` called twice returns the identical string |
| `LineTerminationIsLfNoCrlf` | The output contains zero `\r` characters |

The MCP byte-equality assertion lives in
`tests/Buildout.IntegrationTests/Mcp/SearchToolTests.cs`
(`ToolResultBody_EqualsCliPlainBody`).
