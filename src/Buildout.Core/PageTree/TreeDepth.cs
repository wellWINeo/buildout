using Buildout.Core.PageTree.Errors;

namespace Buildout.Core.PageTree;

public static class TreeDepth
{
    public const int Min = 1;
    public const int Max = 7;
    public const int Default = 3;

    public static int Validate(int depth)
    {
        if (depth < Min || depth > Max)
            throw new TreeDepthOutOfRangeException(depth);
        return depth;
    }
}
