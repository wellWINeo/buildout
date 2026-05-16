namespace Buildout.Core.Markdown.Editing;

public sealed class PartialPatchException : PatchRejectedException
{
    public PartialPatchException(string partialRevision, int committedOpIndex, Exception buildinError)
        : base("patch.partial",
            $"Patch partially applied: {committedOpIndex} operation(s) committed before failure.",
            buildinError,
            new Dictionary<string, object>
            {
                ["partial_revision"] = partialRevision,
                ["committed_op_index"] = committedOpIndex,
                ["buildin_error"] = buildinError,
            })
    {
    }
}
