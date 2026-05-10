using Buildout.Core.DatabaseViews.Rendering;
using Xunit;

namespace Buildout.UnitTests.DatabaseViews.Rendering;

public sealed class CellBudgetTests
{
    [Fact]
    public void Truncate_ShorterThanBudget_ReturnsUnchanged()
    {
        var budget = new CellBudget(10, "…");

        var result = budget.Truncate("hello");

        Assert.Equal("hello", result);
    }

    [Fact]
    public void Truncate_ExactlyAtBudget_ReturnsUnchanged()
    {
        var budget = new CellBudget(5, "…");

        var result = budget.Truncate("hello");

        Assert.Equal("hello", result);
    }

    [Fact]
    public void Truncate_ExceedsBudget_TruncatesWithMarker()
    {
        var budget = new CellBudget(8, "…");

        var result = budget.Truncate("hello world");

        Assert.Equal("hello w…", result);
    }

    [Fact]
    public void Truncate_ExceedsBudget_AppendsMarkerExactlyOnce()
    {
        var budget = new CellBudget(8, "…");
        var marker = "…";

        var result = budget.Truncate("hello world");

        var count = (result.Length - result.Replace(marker, "").Length) / marker.Length;
        Assert.Equal(1, count);
    }

    [Fact]
    public void Truncate_WhitespaceCountsTowardLength()
    {
        var budget = new CellBudget(4, "…");

        var result = budget.Truncate("hi   ");

        Assert.Equal("hi …", result);
    }

    [Fact]
    public void Truncate_EmptyString_ReturnsEmpty()
    {
        var budget = new CellBudget(5, "…");

        var result = budget.Truncate("");

        Assert.Equal("", result);
    }

    [Fact]
    public void Truncate_MultiCharMarker()
    {
        var budget = new CellBudget(8, "...");

        var result = budget.Truncate("hello world");

        Assert.Equal("hello...", result);
    }

    [Fact]
    public void Truncate_SingleCharAtExactBudget_ReturnsUnchanged()
    {
        var budget = new CellBudget(1, "…");

        var result = budget.Truncate("a");

        Assert.Equal("a", result);
    }
}
