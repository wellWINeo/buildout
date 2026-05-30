namespace Buildout.Core.PageTree.Errors;

public sealed class TreeDepthOutOfRangeException : Exception
{
    public TreeDepthOutOfRangeException(int value)
        : base($"depth must be between 1 and 7 (inclusive); got {value}")
    {
    }
}
