namespace Buildout.Core.Markdown.Editing;

public sealed class NoMatchException : PatchRejectedException
{
    public NoMatchException(string oldStr)
        : base("patch.no_match",
            "Patch rejected: old_str not found.",
            new Dictionary<string, object> { ["old_str"] = oldStr })
    {
    }

    public NoMatchException(string oldStr, Exception innerException)
        : base("patch.no_match",
            "Patch rejected: old_str not found.",
            innerException,
            new Dictionary<string, object> { ["old_str"] = oldStr })
    {
    }
}
