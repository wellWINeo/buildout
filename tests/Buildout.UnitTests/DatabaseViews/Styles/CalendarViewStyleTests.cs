using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;
using Xunit;

namespace Buildout.UnitTests.DatabaseViews.Styles;

public sealed class CalendarViewStyleTests
{
    private readonly PropertyValueFormatter _formatter = new();
    private readonly CellBudget _budget = new(24, "…");
    private readonly CalendarViewStyle _sut = new();

    private static Database CreateDatabase(params (string Name, PropertySchema Schema)[] properties)
    {
        return new Database
        {
            Id = "test-db",
            Properties = properties.ToDictionary(p => p.Name, p => p.Schema)
        };
    }

    private static DatabaseRow CreateRow(params (string Name, PropertyValue Value)[] properties)
    {
        return new DatabaseRow("page-id", properties.ToDictionary(p => p.Name, p => p.Value));
    }

    private static DatabaseViewRequest MakeRequest(string dateProperty = "Due") =>
        new("test-db", DatabaseViewStyle.Calendar, null, dateProperty);

    [Fact]
    public void Rows_grouped_under_date_headings_ascending()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Due", new DatePropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Task B" }] }),
                ("Due", new DatePropertyValue { Date = new DateRange { Start = "2025-02-10" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Task A" }] }),
                ("Due", new DatePropertyValue { Date = new DateRange { Start = "2025-01-15" } })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Contains("## 2025-01-15 (Wednesday)", result);
        Assert.Contains("## 2025-02-10 (Monday)", result);

        var idx1 = result.IndexOf("2025-01-15", StringComparison.Ordinal);
        var idx2 = result.IndexOf("2025-02-10", StringComparison.Ordinal);
        Assert.True(idx1 < idx2, "Earlier date heading should appear first");
    }

    [Fact]
    public void Row_with_missing_date_goes_to_undated_section_at_end()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Due", new DatePropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Dated" }] }),
                ("Due", new DatePropertyValue { Date = new DateRange { Start = "2025-01-15" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Undated" }] }),
                ("Due", new DatePropertyValue { Date = null })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Contains("(undated)", result);
        Assert.Contains("Undated", result);

        var datedIdx = result.IndexOf("2025-01-15", StringComparison.Ordinal);
        var undatedIdx = result.IndexOf("(undated)", StringComparison.Ordinal);
        Assert.True(datedIdx < undatedIdx, "(undated) section should appear after dated sections");
    }

    [Fact]
    public void Rows_listed_under_date_heading_as_bullets()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Due", new DatePropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "My Task" }] }),
                ("Due", new DatePropertyValue { Date = new DateRange { Start = "2025-03-20" } })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Contains("## 2025-03-20 (Thursday)", result);
        Assert.Contains("- My Task", result);
    }

    [Fact]
    public void Multiple_rows_on_same_date_grouped_together()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Due", new DatePropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Task 1" }] }),
                ("Due", new DatePropertyValue { Date = new DateRange { Start = "2025-04-01" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Task 2" }] }),
                ("Due", new DatePropertyValue { Date = new DateRange { Start = "2025-04-01" } })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        var headingCount = result.Split("## 2025-04-01").Length - 1;
        Assert.Equal(1, headingCount);
        Assert.Contains("Task 1", result);
        Assert.Contains("Task 2", result);
    }

    [Fact]
    public void Empty_rows_renders_no_rows_message()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Due", new DatePropertySchema())
        );

        var result = _sut.Render(db, [], MakeRequest(), _formatter, _budget);

        Assert.Equal("(no rows)", result);
    }
}
