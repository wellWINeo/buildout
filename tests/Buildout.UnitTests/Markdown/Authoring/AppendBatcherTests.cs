using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring;
using Xunit;

namespace Buildout.UnitTests.Markdown.Authoring;

public class AppendBatcherTests
{
    private static BlockSubtreeWrite MakeSubtree(string id, int childCount = 0)
    {
        var children = Enumerable.Range(0, childCount)
            .Select(i => new BlockSubtreeWrite
            {
                Block = new ParagraphBlock { Id = $"{id}-child-{i}" },
                Children = []
            })
            .ToArray();
        return new BlockSubtreeWrite
        {
            Block = new ParagraphBlock { Id = id },
            Children = children
        };
    }

    [Fact]
    public void Under100Blocks_NoFollowUp()
    {
        var items = Enumerable.Range(0, 50).Select(i => MakeSubtree($"b{i}")).ToArray();
        var batches = AppendBatcher.BatchTopLevel(items);
        Assert.Single(batches);
        Assert.Equal(50, batches[0].Count);
    }

    [Fact]
    public void Over100Blocks_BatchedInOrder()
    {
        var items = Enumerable.Range(0, 250).Select(i => MakeSubtree($"b{i}")).ToArray();
        var batches = AppendBatcher.BatchTopLevel(items);
        Assert.Equal(3, batches.Count);
        Assert.Equal(100, batches[0].Count);
        Assert.Equal(100, batches[1].Count);
        Assert.Equal(50, batches[2].Count);
    }

    [Fact]
    public void NestedChildrenFanout_ReturnsParents()
    {
        var items = new[]
        {
            MakeSubtree("a", 2),
            MakeSubtree("b", 0),
            MakeSubtree("c", 3),
        };
        var withChildren = AppendBatcher.GetItemsWithChildren(items);
        Assert.Equal(2, withChildren.Count);
        Assert.Equal("a", withChildren[0].Block.Id);
        Assert.Equal("c", withChildren[1].Block.Id);
    }

    [Fact]
    public void EmptyBody_NoBatches()
    {
        var batches = AppendBatcher.BatchTopLevel([]);
        Assert.Empty(batches);
    }
}
