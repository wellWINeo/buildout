namespace Buildout.Core.Markdown.Editing;

public sealed class AmbiguousMatchException : PatchRejectedException
{
    public AmbiguousMatchException(string oldStr, int matchCount)
        : base("patch.ambiguous_match",
            $"Patch rejected: old_str matched {matchCount} times.",
            new Dictionary<string, object> { ["old_str"] = oldStr, ["match_count"] = matchCount })
    {
    }

    public AmbiguousMatchException(string oldStr, int matchCount, Exception innerException)
        : base("patch.ambiguous_match",
            $"Patch rejected: old_str matched {matchCount} times.",
            innerException,
            new Dictionary<string, object> { ["old_str"] = oldStr, ["match_count"] = matchCount })
    {
    }
}
