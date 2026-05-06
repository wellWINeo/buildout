using Buildout.Core.Search;
using Xunit;

namespace Buildout.UnitTests.Search;

public sealed class SearchResultFormatterTests
{
    private readonly SearchResultFormatter _formatter = new();

    [Fact]
    public void EmptyList_ReturnsEmptyString()
    {
        var result = _formatter.Format([]);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SingleMatch_ProducesExpectedLine()
    {
        var match = new SearchMatch
        {
            PageId = "abc-123",
            ObjectType = SearchObjectType.Page,
            DisplayTitle = "My Page"
        };

        var result = _formatter.Format([match]);

        Assert.Equal("abc-123\tpage\tMy Page\n", result);
    }

    [Fact]
    public void MultipleMatches_PreserveOrder()
    {
        var matches = new List<SearchMatch>
        {
            new() { PageId = "id-1", ObjectType = SearchObjectType.Page, DisplayTitle = "First" },
            new() { PageId = "id-2", ObjectType = SearchObjectType.Database, DisplayTitle = "Second" },
            new() { PageId = "id-3", ObjectType = SearchObjectType.Page, DisplayTitle = "Third" }
        };

        var result = _formatter.Format(matches);

        Assert.Equal("id-1\tpage\tFirst\nid-2\tdatabase\tSecond\nid-3\tpage\tThird\n", result);
    }

    [Fact]
    public void ObjectTypePage_IsLowerCase()
    {
        var match = new SearchMatch
        {
            PageId = "abc",
            ObjectType = SearchObjectType.Page,
            DisplayTitle = "Title"
        };

        var result = _formatter.Format([match]);

        Assert.Contains("\tpage\t", result);
    }

    [Fact]
    public void ObjectTypeDatabase_IsLowerCase()
    {
        var match = new SearchMatch
        {
            PageId = "abc",
            ObjectType = SearchObjectType.Database,
            DisplayTitle = "Title"
        };

        var result = _formatter.Format([match]);

        Assert.Contains("\tdatabase\t", result);
    }

    [Fact]
    public void UntitledMatch_RendersPlaceholder()
    {
        var match = new SearchMatch
        {
            PageId = "abc",
            ObjectType = SearchObjectType.Page,
            DisplayTitle = "(untitled)"
        };

        var result = _formatter.Format([match]);

        Assert.Equal("abc\tpage\t(untitled)\n", result);
    }

    [Fact]
    public void OutputContainsZeroCr()
    {
        var matches = new List<SearchMatch>
        {
            new() { PageId = "id-1", ObjectType = SearchObjectType.Page, DisplayTitle = "First" },
            new() { PageId = "id-2", ObjectType = SearchObjectType.Database, DisplayTitle = "Second" }
        };

        var result = _formatter.Format(matches);

        Assert.DoesNotContain('\r', result);
    }

    [Fact]
    public void CalledTwice_ReturnsIdenticalOutput()
    {
        var matches = new List<SearchMatch>
        {
            new() { PageId = "id-1", ObjectType = SearchObjectType.Page, DisplayTitle = "First" },
            new() { PageId = "id-2", ObjectType = SearchObjectType.Database, DisplayTitle = "Second" }
        };

        var first = _formatter.Format(matches);
        var second = _formatter.Format(matches);

        Assert.Equal(first, second);
    }
}
