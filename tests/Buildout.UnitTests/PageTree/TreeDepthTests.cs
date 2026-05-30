using Buildout.Core.PageTree;
using Buildout.Core.PageTree.Errors;
using Xunit;

namespace Buildout.UnitTests.PageTree;

public sealed class TreeDepthTests
{
    [Fact]
    public void Constants_HaveCorrectValues()
    {
        Assert.Equal(1, TreeDepth.Min);
        Assert.Equal(7, TreeDepth.Max);
        Assert.Equal(3, TreeDepth.Default);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Validate_AcceptsValidDepths(int depth)
    {
        var result = TreeDepth.Validate(depth);
        Assert.Equal(depth, result);
    }

    [Fact]
    public void Validate_ThrowsForZero()
    {
        var ex = Assert.Throws<TreeDepthOutOfRangeException>(() => TreeDepth.Validate(0));
        Assert.Contains("0", ex.Message);
        Assert.Contains("1", ex.Message);
        Assert.Contains("7", ex.Message);
    }

    [Fact]
    public void Validate_ThrowsForEight()
    {
        var ex = Assert.Throws<TreeDepthOutOfRangeException>(() => TreeDepth.Validate(8));
        Assert.Contains("8", ex.Message);
        Assert.Contains("1", ex.Message);
        Assert.Contains("7", ex.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(9)]
    [InlineData(100)]
    public void Validate_ThrowsForOutOfRangeValues(int depth)
    {
        var ex = Assert.Throws<TreeDepthOutOfRangeException>(() => TreeDepth.Validate(depth));
        Assert.Contains(depth.ToString(System.Globalization.CultureInfo.InvariantCulture), ex.Message);
    }

    [Fact]
    public void ExceptionMessage_MatchesExactFormat()
    {
        var ex = Assert.Throws<TreeDepthOutOfRangeException>(() => TreeDepth.Validate(0));
        Assert.Equal("depth must be between 1 and 7 (inclusive); got 0", ex.Message);
    }
}
