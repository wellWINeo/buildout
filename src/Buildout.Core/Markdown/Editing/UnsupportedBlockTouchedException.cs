namespace Buildout.Core.Markdown.Editing;

public sealed class UnsupportedBlockTouchedException : PatchRejectedException
{
    public UnsupportedBlockTouchedException(string anchor)
        : base("patch.unsupported_block_touched",
            $"Patch rejected: unsupported block touched at anchor '{anchor}'.",
            new Dictionary<string, object> { ["anchor"] = anchor })
    {
    }

    public UnsupportedBlockTouchedException(string anchor, Exception innerException)
        : base("patch.unsupported_block_touched",
            $"Patch rejected: unsupported block touched at anchor '{anchor}'.",
            innerException,
            new Dictionary<string, object> { ["anchor"] = anchor })
    {
    }
}
