using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;
using Xunit;

namespace Buildout.UnitTests.DatabaseViews.Styles;

public sealed class TimelineViewStyleTests
{
    private readonly PropertyValueFormatter _formatter = new();
    private readonly CellBudget _budget = new(24, "…");
    private readonly TimelineViewStyle _sut = new();

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

    private static DatabaseViewRequest MakeRequest(string dateProperty = "Phase") =>
        new("test-db", DatabaseViewStyle.Timeline, null, dateProperty);

    [Fact]
    public void Rows_grouped_by_year_month_headings_ascending()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Phase", new DatePropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Task B" }] }),
                ("Phase", new DatePropertyValue { Date = new DateRange { Start = "2025-03-01" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Task A" }] }),
                ("Phase", new DatePropertyValue { Date = new DateRange { Start = "2025-01-15" } })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Contains("## 2025-01", result);
        Assert.Contains("## 2025-03", result);

        var idx1 = result.IndexOf("## 2025-01", StringComparison.Ordinal);
        var idx2 = result.IndexOf("## 2025-03", StringComparison.Ordinal);
        Assert.True(idx1 < idx2, "Earlier month heading should appear first");
    }

    [Fact]
    public void Entry_with_start_and_end_renders_range_with_duration()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Phase", new DatePropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Sprint 1" }] }),
                ("Phase", new DatePropertyValue { Date = new DateRange { Start = "2025-01-01", End = "2025-01-14" } })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Contains("2025-01-01 → 2025-01-14 (14d)", result);
        Assert.Contains("Sprint 1", result);
    }

    [Fact]
    public void Entry_with_only_start_renders_as_one_day()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Phase", new DatePropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Milestone" }] }),
                ("Phase", new DatePropertyValue { Date = new DateRange { Start = "2025-02-15" } })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Contains("2025-02-15 (1d)", result);
        Assert.Contains("Milestone", result);
    }

    [Fact]
    public void Row_with_no_start_date_goes_to_undated_section_at_end()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Phase", new DatePropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Dated" }] }),
                ("Phase", new DatePropertyValue { Date = new DateRange { Start = "2025-01-10" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Undated" }] }),
                ("Phase", new DatePropertyValue { Date = null })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Contains("(undated)", result);
        Assert.Contains("Undated", result);

        var datedIdx = result.IndexOf("2025-01", StringComparison.Ordinal);
        var undatedIdx = result.IndexOf("(undated)", StringComparison.Ordinal);
        Assert.True(datedIdx < undatedIdx, "(undated) section should appear after dated sections");
    }

    [Fact]
    public void Empty_rows_renders_no_rows_message()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Phase", new DatePropertySchema())
        );

        var result = _sut.Render(db, [], MakeRequest(), _formatter, _budget);

        Assert.Equal("(no rows)", result);
    }

    [Fact]
    public void Multiple_rows_in_same_month_grouped_under_one_heading()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Phase", new DatePropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Task 1" }] }),
                ("Phase", new DatePropertyValue { Date = new DateRange { Start = "2025-06-05" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Task 2" }] }),
                ("Phase", new DatePropertyValue { Date = new DateRange { Start = "2025-06-20" } })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        var headingCount = result.Split("## 2025-06").Length - 1;
        Assert.Equal(1, headingCount);
        Assert.Contains("Task 1", result);
        Assert.Contains("Task 2", result);
    }
}
