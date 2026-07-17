namespace Buildout.Core.Markdown.Editing;

public sealed class PartialPatchException : PatchRejectedException
{
    public PartialPatchException(string partialRevision, int committedOpIndex, Exception buildinError)
        : base("patch.partial",
            BuildMessage(committedOpIndex, buildinError),
            buildinError,
            new Dictionary<string, object>
            {
                ["partial_revision"] = partialRevision,
                ["committed_op_index"] = committedOpIndex,
                ["buildin_error"] = buildinError,
            })
    {
    }

    private static string BuildMessage(int committedOpIndex, Exception buildinError) =>
        $"Patch partially applied: {committedOpIndex} operation(s) committed before failure. " +
        $"Underlying error: {buildinError.Message}";
}
