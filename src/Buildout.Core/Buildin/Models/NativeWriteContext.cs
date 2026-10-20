namespace Buildout.Core.Buildin.Models;

/// <summary>Per-invocation native write metadata; never persisted or rendered.</summary>
public sealed record NativeWriteContext
{
    public string? IdempotencyKey { get; init; }
    public string? ETag { get; init; }
}
