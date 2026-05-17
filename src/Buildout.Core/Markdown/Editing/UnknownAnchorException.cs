namespace Buildout.Core.Markdown.Editing;

public sealed class UnknownAnchorException : PatchRejectedException
{
    public UnknownAnchorException(string anchor)
        : base("patch.unknown_anchor",
            $"Patch rejected: unknown anchor '{anchor}'.",
            new Dictionary<string, object> { ["anchor"] = anchor })
    {
    }

    public UnknownAnchorException(string anchor, Exception innerException)
        : base("patch.unknown_anchor",
            $"Patch rejected: unknown anchor '{anchor}'.",
            innerException,
            new Dictionary<string, object> { ["anchor"] = anchor })
    {
    }
}
