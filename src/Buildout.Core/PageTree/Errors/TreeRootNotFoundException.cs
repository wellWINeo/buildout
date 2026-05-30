using Buildout.Core.Buildin.Errors;

namespace Buildout.Core.PageTree.Errors;

public sealed class TreeRootNotFoundException : Exception
{
    public TreeRootNotFoundException(string id, BuildinApiException inner)
        : base($"page or database not found: {id}", inner)
    {
    }
}
