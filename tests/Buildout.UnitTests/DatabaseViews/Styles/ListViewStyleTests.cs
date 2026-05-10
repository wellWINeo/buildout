using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;
using Xunit;

namespace Buildout.UnitTests.DatabaseViews.Styles;

public sealed class ListViewStyleTests
{
    private readonly PropertyValueFormatter _formatter = new();
    private readonly CellBudget _budget = new(24, "…");
    private readonly ListViewStyle _sut = new();

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

    private static DatabaseViewRequest MakeRequest() =>
        new("test-db", DatabaseViewStyle.List, null, null);

    [Fact]
    public void One_bullet_per_row_with_title_and_properties()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Status", new SelectPropertySchema()),
            ("Priority", new NumberPropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "My Task" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "1", Name = "Active" } }),
                ("Priority", new NumberPropertyValue { Number = 1 })
            )
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Equal("- My Task (Status: Active, Priority: 1)", result);
    }

    [Fact]
    public void No_non_title_props_renders_title_only_bullet()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Solo Task" }] })
            )
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Equal("- Solo Task", result);
    }

    [Fact]
    public void Multiple_rows_each_on_separate_line()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Status", new SelectPropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Row 1" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "1", Name = "Active" } })
            ),
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Row 2" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "2", Name = "Done" } })
            )
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Equal("- Row 1 (Status: Active)\n- Row 2 (Status: Done)", result);
    }

    [Fact]
    public void Empty_rows_renders_no_rows_message()
    {
        var db = CreateDatabase(("Name", new TitlePropertySchema()));

        var result = _sut.Render(db, [], MakeRequest(), _formatter, _budget);

        Assert.Equal("(no rows)", result);
    }

    [Fact]
    public void Missing_property_value_renders_em_dash()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Status", new SelectPropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Row" }] }),
                ("Status", new SelectPropertyValue { Select = null })
            )
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Equal("- Row (Status: —)", result);
    }
}
