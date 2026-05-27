# Contract: ContextualTokenProvider

**Feature**: `014-mcp-authorization` | **Date**: 2025-05-27

## Location

`src/Buildout.Core/Buildin/Authentication/ContextualTokenProvider.cs`

## Signature

```csharp
namespace Buildout.Core.Buildin.Authentication;

public sealed class ContextualTokenProvider : BaseBearerTokenAuthenticationProvider
{
    public ContextualTokenProvider(string defaultBotToken);
    public static IDisposable OverrideToken(string token);
}
```

## Contract

### Constructor

Creates a provider that returns `defaultBotToken` when no override is active.

### OverrideToken

Sets a per-async-flow token override. Returns an `IDisposable` that restores the previous value when disposed.

**Thread safety**: Uses `AsyncLocal<string?>` — each async flow has its own copy. No cross-request interference.

**Usage pattern**:
```csharp
using var _ = ContextualTokenProvider.OverrideToken(resolvedToken);
var result = await buildinClient.GetPageAsync(pageId, ct);
// override is automatically cleared when the scope exits
```

## Integration

Replaces `BotTokenAuthenticationProvider` in DI registration when auth is enabled:

```csharp
// ServiceCollectionExtensions.AddBuildinClient — modified
services.AddSingleton<IAuthenticationProvider>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<BuildinClientOptions>>().Value;
    return new ContextualTokenProvider(opts.BotToken);
});
```

In `none` mode, `OverrideToken` is never called — the default token is used for all requests (identical to current behavior).

## Rationale

- `AsyncLocal` flows through async call chains naturally — no ASP.NET Core dependency in `Buildout.Core`.
- `IDisposable` scope prevents token leaks across request boundaries.
- Replaces the existing `BotTokenAuthenticationProvider` registration — no new DI key needed.
