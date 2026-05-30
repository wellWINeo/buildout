namespace Buildout.Core.PageTree.Rendering;

public interface ITreeRenderer
{
    TreeFormat Format { get; }
    string Render(TreeNode root);
}
