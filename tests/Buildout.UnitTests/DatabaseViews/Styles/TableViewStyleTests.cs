using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;
using Xunit;

namespace Buildout.UnitTests.DatabaseViews.Styles;

public sealed class TableViewStyleTests
{
    private readonly PropertyValueFormatter _formatter = new();
    private readonly CellBudget _budget = new(24, "\u2026");
    private readonly TableViewStyle _sut = new();

    private static Database CreateDatabase(params (string Name, PropertySchema Schema)[] properties)
    {
        return new Database
        {
            Id = "test-db",
            Title = [new RichText { Type = "text", Content = "Test DB" }],
            Properties = properties.ToDictionary(p => p.Name, p => p.Schema)
        };
    }

    private static DatabaseRow CreateRow(params (string Name, PropertyValue Value)[] properties)
    {
        return new DatabaseRow("page-id", properties.ToDictionary(p => p.Name, p => p.Value));
    }

    private static DatabaseViewRequest MakeRequest() =>
        new("test-db", DatabaseViewStyle.Table, null, null);

    [Fact]
    public void Three_row_fixture_renders_pipe_table()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Status", new SelectPropertySchema()),
            ("Due", new DatePropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue
                {
                    Title = [new RichText { Type = "text", Content = "Task 1" }]
                }),
                ("Status", new SelectPropertyValue
                {
                    Select = new SelectOption { Id = "s1", Name = "Todo" }
                }),
                ("Due", new DatePropertyValue
                {
                    Date = new DateRange { Start = "2025-01-15" }
                })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue
                {
                    Title = [new RichText { Type = "text", Content = "Task 2" }]
                }),
                ("Status", new SelectPropertyValue
                {
                    Select = new SelectOption { Id = "s2", Name = "Done" }
                }),
                ("Due", new DatePropertyValue
                {
                    Date = new DateRange { Start = "2025-01-20" }
                })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue
                {
                    Title = [new RichText { Type = "text", Content = "Task 3" }]
                }),
                ("Status", new SelectPropertyValue { Select = null }),
                ("Due", new DatePropertyValue { Date = null })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Equal("""
            | Name | Status | Due |
            | --- | --- | --- |
            | Task 1 | Todo | 2025-01-15 |
            | Task 2 | Done | 2025-01-20 |
            | Task 3 | — | — |
            """, result);
    }

    [Fact]
    public void Wide_fixture_switches_to_stacked()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Alpha", new RichTextPropertySchema()),
            ("Beta", new RichTextPropertySchema()),
            ("Gamma", new RichTextPropertySchema()),
            ("Delta", new RichTextPropertySchema()),
            ("Epsilon", new RichTextPropertySchema()),
            ("Zeta", new RichTextPropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue
                {
                    Title = [new RichText { Type = "text", Content = "Wide Item" }]
                }),
                ("Alpha", new RichTextPropertyValue
                {
                    RichText = [new RichText { Type = "text", Content = "a" }]
                }),
                ("Beta", new RichTextPropertyValue
                {
                    RichText = [new RichText { Type = "text", Content = "b" }]
                }),
                ("Gamma", new RichTextPropertyValue
                {
                    RichText = [new RichText { Type = "text", Content = "g" }]
                }),
                ("Delta", new RichTextPropertyValue
                {
                    RichText = [new RichText { Type = "text", Content = "d" }]
                }),
                ("Epsilon", new RichTextPropertyValue
                {
                    RichText = [new RichText { Type = "text", Content = "e" }]
                }),
                ("Zeta", new RichTextPropertyValue
                {
                    RichText = [new RichText { Type = "text", Content = "z" }]
                })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Equal("""
            ─ Wide Item
                Alpha: a
                Beta: b
                Gamma: g
                Delta: d
                Epsilon: e
                Zeta: z
            """, result);
    }

    [Fact]
    public void Empty_fixture_renders_no_rows_message()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Status", new SelectPropertySchema())
        );

        var rows = new List<DatabaseRow>();

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Equal("(no rows)", result);
    }

    [Fact]
    public void Title_only_renders_single_column()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue
                {
                    Title = [new RichText { Type = "text", Content = "Solo 1" }]
                })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue
                {
                    Title = [new RichText { Type = "text", Content = "Solo 2" }]
                })
            ),
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Equal("""
            | Name |
            | --- |
            | Solo 1 |
            | Solo 2 |
            """, result);
    }
}
