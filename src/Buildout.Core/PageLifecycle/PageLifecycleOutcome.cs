namespace Buildout.Core.PageLifecycle;

using Buildout.Core.Markdown.Authoring;

public sealed record PageLifecycleOutcome
{
    public required string PageId { get; init; }

    public bool? Archived { get; init; }

    public required bool Changed { get; init; }

    public FailureClass? FailureClass { get; init; }

    public Exception? UnderlyingException { get; init; }
}
