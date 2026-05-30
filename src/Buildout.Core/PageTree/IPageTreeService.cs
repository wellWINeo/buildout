namespace Buildout.Core.PageTree;

public interface IPageTreeService
{
    Task<TreeNode> BuildAsync(string targetId, int depth, CancellationToken cancellationToken = default);
}
