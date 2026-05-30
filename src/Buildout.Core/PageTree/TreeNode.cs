namespace Buildout.Core.PageTree;

public sealed record TreeNode(
    string Name,
    string Uri,
    IReadOnlyList<TreeNode> Children);
