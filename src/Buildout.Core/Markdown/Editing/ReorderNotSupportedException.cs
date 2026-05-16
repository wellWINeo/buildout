namespace Buildout.Core.Markdown.Editing;

public sealed class ReorderNotSupportedException : PatchRejectedException
{
    public ReorderNotSupportedException(string anchor, int oldPosition, int newPosition)
        : base("patch.reorder_not_supported",
            $"Patch rejected: reorder not supported for anchor '{anchor}'.",
            new Dictionary<string, object>
            {
                ["anchor"] = anchor,
                ["old_position"] = oldPosition,
                ["new_position"] = newPosition,
            })
    {
    }

    public ReorderNotSupportedException(string anchor, int oldPosition, int newPosition, Exception innerException)
        : base("patch.reorder_not_supported",
            $"Patch rejected: reorder not supported for anchor '{anchor}'.",
            innerException,
            new Dictionary<string, object>
            {
                ["anchor"] = anchor,
                ["old_position"] = oldPosition,
                ["new_position"] = newPosition,
            })
    {
    }
}
