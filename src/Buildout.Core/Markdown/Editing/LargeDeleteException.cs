namespace Buildout.Core.Markdown.Editing;

public sealed class LargeDeleteException : PatchRejectedException
{
    public LargeDeleteException(int wouldDelete, int threshold)
        : base("patch.large_delete",
            $"Patch rejected: would delete {wouldDelete} characters, exceeding threshold of {threshold}.",
            new Dictionary<string, object> { ["would_delete"] = wouldDelete, ["threshold"] = threshold })
    {
    }

    public LargeDeleteException(int wouldDelete, int threshold, Exception innerException)
        : base("patch.large_delete",
            $"Patch rejected: would delete {wouldDelete} characters, exceeding threshold of {threshold}.",
            innerException,
            new Dictionary<string, object> { ["would_delete"] = wouldDelete, ["threshold"] = threshold })
    {
    }
}
