namespace Buildout.Core.Markdown.Authoring;

public enum FailureClass
{
    Validation,
    NotFound,
    Auth,
    Transport,
    Unexpected,
    Partial
}

public sealed record CreatePageOutcome
{
    public required string NewPageId { get; init; }
    public string? ResolvedTitle { get; init; }
    public string? PartialPageId { get; init; }
    public FailureClass? FailureClass { get; init; }
    public Exception? UnderlyingException { get; init; }
}

public sealed class PartialCreationException : Exception
{
    public string NewPageId { get; }
    public int BatchesAppended { get; }
    public int TotalBatches { get; }

    public PartialCreationException(string newPageId, int batchesAppended, int totalBatches, Exception underlying)
        : base($"Partial creation: page {newPageId} exists but appendBlockChildren failed after {batchesAppended} of {totalBatches} top-level batches: {underlying.Message}", underlying)
    {
        NewPageId = newPageId;
        BatchesAppended = batchesAppended;
        TotalBatches = totalBatches;
    }
}
