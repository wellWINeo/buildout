# Contract: IRequestAuthenticator Interface

**Feature**: `014-mcp-authorization` | **Date**: 2025-05-27

## Location

`src/Buildout.Core/Auth/IRequestAuthenticator.cs`

## Signature

```csharp
namespace Buildout.Core.Auth;

public interface IRequestAuthenticator
{
    Task<AuthResult> AuthenticateAsync(string? authorizationHeader);
}
```

## Contract

### AuthenticateAsync

Validates the incoming credentials and resolves the Buildin Bot API key for outbound calls.

**Parameters**:
- `authorizationHeader`: The value of the `Authorization` header from the incoming MCP HTTP request. May be null if no header was provided.

**Preconditions**:
- The `AuthOptions` have been validated at startup (mode, provider, connection string).

**Postconditions**:
- On success: `AuthResult.IsAuthenticated` is `true`, `ResolvedBotToken` is non-null, `ErrorMessage` is null.
- On failure: `AuthResult.IsAuthenticated` is `false`, `ResolvedBotToken` is null, `ErrorMessage` describes the failure.
- The method MUST NOT throw exceptions for expected authentication failures (missing header, invalid token, revoked token). Unexpected errors (database unreachable) MAY throw.

**Thread safety**: Implementations MUST be safe for concurrent calls.

**Performance**: MUST complete in <5ms for proxy/mapped modes (SC-004).

## Mode Behavior

| Mode | `authorizationHeader` null | `authorizationHeader` present (valid) | `authorizationHeader` present (invalid) |
|------|---------------------------|--------------------------------------|----------------------------------------|
| `None` | Success (global BotToken) | Success (global BotToken, header ignored) | Success (global BotToken, header ignored) |
| `Passthrough` | Failure: "Authorization header required" | Success (use provided key) | Success (use provided key; Buildin API will reject) |
| `Proxy` | Failure: "Authorization header required" | Success if MCP token valid (global BotToken) | Failure: "Invalid or revoked token" |
| `Mapped` | Failure: "Authorization header required" | Success if MCP token valid + mapped key exists (mapped BotToken) | Failure: "Invalid or revoked token" / "Token has no mapped Buildin key" |

## Implementations

| Class | Project | Registered When |
|-------|---------|-----------------|
| `NoneAuthenticator` | `Buildout.Mcp` | `Auth:Mode=None` (default) |
| `PassthroughAuthenticator` | `Buildout.Mcp` | `Auth:Mode=Passthrough` |
| `ProxyAuthenticator` | `Buildout.Mcp` | `Auth:Mode=Proxy` |
| `MappedAuthenticator` | `Buildout.Mcp` | `Auth:Mode=Mapped` |

## DI Registration

Registered in `Buildout.Mcp` via `AddAuth(this IServiceCollection, IConfiguration, bool)` extension method.

```csharp
public static IServiceCollection AddAuth(
    this IServiceCollection services,
    IConfiguration configuration,
    bool isHttpTransport)
{
    services.AddOptions<AuthOptions>()
        .Bind(configuration.GetSection("Auth"))
        .ValidateOnStart();
    services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();

    // mode-specific registration...
}
```
