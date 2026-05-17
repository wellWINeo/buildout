namespace Buildout.Core.Markdown.Editing;

public sealed class StaleRevisionException : PatchRejectedException
{
    public StaleRevisionException(string currentRevision)
        : base("patch.stale_revision",
            $"Patch rejected: stale revision (current revision: {currentRevision}).",
            new Dictionary<string, object> { ["current_revision"] = currentRevision })
    {
    }

    public StaleRevisionException(string currentRevision, Exception innerException)
        : base("patch.stale_revision",
            $"Patch rejected: stale revision (current revision: {currentRevision}).",
            innerException,
            new Dictionary<string, object> { ["current_revision"] = currentRevision })
    {
    }
}
