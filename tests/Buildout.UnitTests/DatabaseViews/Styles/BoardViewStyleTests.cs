using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;
using Xunit;

namespace Buildout.UnitTests.DatabaseViews.Styles;

public sealed class BoardViewStyleTests
{
    private readonly PropertyValueFormatter _formatter = new();
    private readonly CellBudget _budget = new(24, "…");
    private readonly BoardViewStyle _sut = new();

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

    private static DatabaseViewRequest MakeRequest(string? groupBy = "Status") =>
        new("test-db", DatabaseViewStyle.Board, groupBy, null);

    [Fact]
    public void Three_non_empty_groups_renders_side_by_side_columns()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Status", new SelectPropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Task A" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "1", Name = "Todo" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Task B" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "2", Name = "Doing" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Task C" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "3", Name = "Done" } })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Contains("Todo", result);
        Assert.Contains("Doing", result);
        Assert.Contains("Done", result);
        Assert.Contains("Task A", result);
        Assert.Contains("Task B", result);
        Assert.Contains("Task C", result);

        var lines = result.Split('\n');
        var headerLine = lines.FirstOrDefault(l => l.Contains("Todo") && l.Contains("Doing") && l.Contains("Done"));
        Assert.NotNull(headerLine);
    }

    [Fact]
    public void More_than_three_groups_renders_stacked_sections()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Status", new SelectPropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Task A" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "1", Name = "Backlog" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Task B" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "2", Name = "Todo" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Task C" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "3", Name = "Doing" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Task D" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "4", Name = "Done" } })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Contains("### Backlog (1 row)", result);
        Assert.Contains("### Todo (1 row)", result);
        Assert.Contains("### Doing (1 row)", result);
        Assert.Contains("### Done (1 row)", result);
        Assert.Contains("Task A", result);
        Assert.Contains("Task B", result);
        Assert.Contains("Task C", result);
        Assert.Contains("Task D", result);
    }

    [Fact]
    public void Row_with_missing_group_by_value_lands_under_none_group()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Status", new SelectPropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Ungrouped" }] }),
                ("Status", new SelectPropertyValue { Select = null })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Contains("(none)", result);
        Assert.Contains("Ungrouped", result);
    }

    [Fact]
    public void None_group_appended_last_when_other_groups_present()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Status", new SelectPropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Named" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "1", Name = "Active" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Unnamed" }] }),
                ("Status", new SelectPropertyValue { Select = null })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        var activeIndex = result.IndexOf("Active", StringComparison.Ordinal);
        var noneIndex = result.IndexOf("(none)", StringComparison.Ordinal);
        Assert.True(activeIndex < noneIndex, "(none) should appear after named groups");
    }

    [Fact]
    public void Multi_select_group_key_is_comma_joined_option_names()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Tags", new MultiSelectPropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Multi-tagged" }] }),
                ("Tags", new MultiSelectPropertyValue
                {
                    MultiSelect =
                    [
                        new SelectOption { Id = "1", Name = "Alpha" },
                        new SelectOption { Id = "2", Name = "Beta" }
                    ]
                })
            ),
        };

        var result = _sut.Render(db, rows, new DatabaseViewRequest("test-db", DatabaseViewStyle.Board, "Tags", null), _formatter, _budget);

        Assert.Contains("Alpha, Beta", result);
        Assert.Contains("Multi-tagged", result);
    }

    [Fact]
    public void Checkbox_groups_as_checked_unchecked()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Done", new CheckboxPropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Complete" }] }),
                ("Done", new CheckboxPropertyValue { Checkbox = true })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Incomplete" }] }),
                ("Done", new CheckboxPropertyValue { Checkbox = false })
            ),
        };

        var result = _sut.Render(db, rows, new DatabaseViewRequest("test-db", DatabaseViewStyle.Board, "Done", null), _formatter, _budget);

        Assert.Contains("Checked", result);
        Assert.Contains("Unchecked", result);
        Assert.Contains("Complete", result);
        Assert.Contains("Incomplete", result);
    }

    [Fact]
    public void Stacked_groups_show_row_count()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Status", new SelectPropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "A1" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "1", Name = "G1" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "A2" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "1", Name = "G1" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "B1" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "2", Name = "G2" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "C1" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "3", Name = "G3" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "D1" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "4", Name = "G4" } })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Contains("### G1 (2 rows)", result);
        Assert.Contains("### G2 (1 row)", result);
    }
}
