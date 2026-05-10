using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;
using Xunit;

namespace Buildout.UnitTests.DatabaseViews.Styles;

public sealed class GalleryViewStyleTests
{
    private readonly PropertyValueFormatter _formatter = new();
    private readonly CellBudget _budget = new(24, "…");
    private readonly GalleryViewStyle _sut = new();

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
        new("test-db", DatabaseViewStyle.Gallery, null, null);

    [Fact]
    public void Single_row_renders_card_with_cover_placeholder_and_title()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "My Item" }] })
            )
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Contains("[cover: none]", result);
        Assert.Contains("My Item", result);
        Assert.Contains("┌", result);
        Assert.Contains("└", result);
    }

    [Fact]
    public void Card_shows_at_most_three_secondary_properties()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Alpha", new RichTextPropertySchema()),
            ("Beta", new RichTextPropertySchema()),
            ("Gamma", new RichTextPropertySchema()),
            ("Delta", new RichTextPropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Item" }] }),
                ("Alpha", new RichTextPropertyValue { RichText = [new RichText { Type = "text", Content = "a" }] }),
                ("Beta", new RichTextPropertyValue { RichText = [new RichText { Type = "text", Content = "b" }] }),
                ("Gamma", new RichTextPropertyValue { RichText = [new RichText { Type = "text", Content = "g" }] }),
                ("Delta", new RichTextPropertyValue { RichText = [new RichText { Type = "text", Content = "d" }] })
            )
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Contains("Alpha: a", result);
        Assert.Contains("Beta: b", result);
        Assert.Contains("Gamma: g", result);
        Assert.DoesNotContain("Delta: d", result);
    }

    [Fact]
    public void Multiple_rows_each_get_a_card_separated_by_blank_line()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Card One" }] })),
            CreateRow(("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Card Two" }] }))
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        Assert.Contains("Card One", result);
        Assert.Contains("Card Two", result);
        Assert.Contains("\n\n", result);
    }

    [Fact]
    public void Empty_rows_renders_no_rows_message()
    {
        var db = CreateDatabase(("Name", new TitlePropertySchema()));

        var result = _sut.Render(db, [], MakeRequest(), _formatter, _budget);

        Assert.Equal("(no rows)", result);
    }

    [Fact]
    public void Secondary_props_shown_in_schema_order_after_title()
    {
        var db = CreateDatabase(
            ("Name", new TitlePropertySchema()),
            ("Status", new SelectPropertySchema()),
            ("Notes", new RichTextPropertySchema())
        );

        var rows = new List<DatabaseRow>
        {
            CreateRow(
                ("Name", new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Row" }] }),
                ("Status", new SelectPropertyValue { Select = new SelectOption { Id = "1", Name = "Active" } }),
                ("Notes", new RichTextPropertyValue { RichText = [new RichText { Type = "text", Content = "A note" }] })
            )
        };

        var result = _sut.Render(db, rows, MakeRequest(), _formatter, _budget);

        var statusIdx = result.IndexOf("Status:", StringComparison.Ordinal);
        var notesIdx = result.IndexOf("Notes:", StringComparison.Ordinal);
        Assert.True(statusIdx < notesIdx, "Status should appear before Notes in schema order");
    }
}
