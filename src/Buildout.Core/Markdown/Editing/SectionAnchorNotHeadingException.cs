namespace Buildout.Core.Markdown.Editing;

public sealed class SectionAnchorNotHeadingException : PatchRejectedException
{
    public SectionAnchorNotHeadingException(string anchor)
        : base("patch.section_anchor_not_heading",
            $"Patch rejected: section anchor '{anchor}' is not a heading.",
            new Dictionary<string, object> { ["anchor"] = anchor })
    {
    }

    public SectionAnchorNotHeadingException(string anchor, Exception innerException)
        : base("patch.section_anchor_not_heading",
            $"Patch rejected: section anchor '{anchor}' is not a heading.",
            innerException,
            new Dictionary<string, object> { ["anchor"] = anchor })
    {
    }
}
