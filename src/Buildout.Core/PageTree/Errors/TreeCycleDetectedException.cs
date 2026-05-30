namespace Buildout.Core.PageTree.Errors;

public sealed class TreeCycleDetectedException : Exception
{
    public TreeCycleDetectedException(string id)
        : base($"cycle detected in page hierarchy at node {id}")
    {
    }
}
