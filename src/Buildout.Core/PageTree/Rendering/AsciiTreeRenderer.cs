using System.Text;

namespace Buildout.Core.PageTree.Rendering;

public sealed class AsciiTreeRenderer : ITreeRenderer
{
    public TreeFormat Format => TreeFormat.Ascii;

    public string Render(TreeNode root)
    {
        var sb = new StringBuilder();
        RenderNode(sb, root, prefix: "", isLast: true, isRoot: true);
        return sb.ToString();
    }

    private static void RenderNode(StringBuilder sb, TreeNode node, string prefix, bool isLast, bool isRoot)
    {
        if (sb.Length > 0)
            sb.Append('\n');

        if (!isRoot)
        {
            sb.Append(prefix);
            sb.Append(isLast ? "└── " : "├── ");
        }

        var escapedName = EscapeName(node.Name);
        sb.Append('[');
        sb.Append(escapedName);
        sb.Append("](<");
        sb.Append(node.Uri);
        sb.Append(">)");

        var childPrefix = isRoot ? "" : prefix + (isLast ? "    " : "│   ");

        for (var i = 0; i < node.Children.Count; i++)
        {
            var isChildLast = i == node.Children.Count - 1;
            RenderNode(sb, node.Children[i], childPrefix, isChildLast, isRoot: false);
        }
    }

    private static string EscapeName(string name)
    {
        name = name.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');

        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (ch is '\\' or '[' or ']' or '<' or '>')
                sb.Append('\\');
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
