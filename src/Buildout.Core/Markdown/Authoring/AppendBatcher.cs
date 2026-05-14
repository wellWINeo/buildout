using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Authoring;

public sealed class AppendBatcher
{
    private const int MaxBatchSize = 100;

    public static IReadOnlyList<IReadOnlyList<Block>> BatchTopLevel(IReadOnlyList<BlockSubtreeWrite> subtrees)
    {
        if (subtrees.Count == 0) return [];

        var batches = new List<IReadOnlyList<Block>>();
        for (int i = 0; i < subtrees.Count; i += MaxBatchSize)
        {
            var batch = subtrees.Skip(i).Take(MaxBatchSize).Select(s => s.Block).ToArray();
            batches.Add(batch);
        }
        return batches;
    }

    public static IReadOnlyList<BlockSubtreeWrite> GetItemsWithChildren(IReadOnlyList<BlockSubtreeWrite> subtrees)
    {
        return subtrees.Where(s => s.Children.Count > 0).ToArray();
    }
}
