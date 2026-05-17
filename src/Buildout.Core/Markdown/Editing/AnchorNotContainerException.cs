namespace Buildout.Core.Markdown.Editing;

public sealed class AnchorNotContainerException : PatchRejectedException
{
    public AnchorNotContainerException(string anchor, string blockType)
        : base("patch.anchor_not_container",
            $"Patch rejected: anchor '{anchor}' is not a container block (type: {blockType}).",
            new Dictionary<string, object> { ["anchor"] = anchor, ["block_type"] = blockType })
    {
    }

    public AnchorNotContainerException(string anchor, string blockType, Exception innerException)
        : base("patch.anchor_not_container",
            $"Patch rejected: anchor '{anchor}' is not a container block (type: {blockType}).",
            innerException,
            new Dictionary<string, object> { ["anchor"] = anchor, ["block_type"] = blockType })
    {
    }
}
