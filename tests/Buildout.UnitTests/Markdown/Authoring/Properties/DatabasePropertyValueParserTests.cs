using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Properties;
using Xunit;

namespace Buildout.UnitTests.Markdown.Authoring.Properties;

public class DatabasePropertyValueParserTests
{
    private readonly DatabasePropertyValueParser _sut = new();

    [Fact]
    public void Title_PassesThrough()
    {
        var result = _sut.Parse("Name", "Hello", new TitlePropertySchema());
        var title = Assert.IsType<TitlePropertyValue>(result);
        Assert.Equal("Hello", title.Title![0].Content);
    }

    [Fact]
    public void RichText_PassesThrough()
    {
        var result = _sut.Parse("Notes", "Some text", new RichTextPropertySchema());
        var rt = Assert.IsType<RichTextPropertyValue>(result);
        Assert.Equal("Some text", rt.RichText![0].Content);
    }

    [Fact]
    public void Number_ValidDouble_ReturnsNumberValue()
    {
        var result = _sut.Parse("Count", "42.5", new NumberPropertySchema());
        var num = Assert.IsType<NumberPropertyValue>(result);
        Assert.Equal(42.5, num.Number);
    }

    [Fact]
    public void Number_InvalidInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => _sut.Parse("Count", "abc", new NumberPropertySchema()));
    }

    [Fact]
    public void Select_ValidOption_ReturnsSelectValue()
    {
        var schema = new SelectPropertySchema
        {
            Options = [new SelectOption { Id = "1", Name = "Done" }, new SelectOption { Id = "2", Name = "Todo" }]
        };
        var result = _sut.Parse("Status", "Done", schema);
        var select = Assert.IsType<SelectPropertyValue>(result);
        Assert.Equal("Done", select.Select!.Name);
    }

    [Fact]
    public void Select_InvalidOption_Throws()
    {
        var schema = new SelectPropertySchema { Options = [new SelectOption { Id = "1", Name = "Done" }] };
        Assert.Throws<ArgumentException>(() => _sut.Parse("Status", "Unknown", schema));
    }

    [Fact]
    public void MultiSelect_CommaSplit_ReturnsMultipleOptions()
    {
        var schema = new MultiSelectPropertySchema
        {
            Options = [new SelectOption { Id = "1", Name = "red" }, new SelectOption { Id = "2", Name = "green" }, new SelectOption { Id = "3", Name = "blue" }]
        };
        var result = _sut.Parse("Tags", "red,green", schema);
        var multi = Assert.IsType<MultiSelectPropertyValue>(result);
        Assert.Equal(2, multi.MultiSelect!.Count);
    }

    [Fact]
    public void Checkbox_True_ReturnsTrue()
    {
        var result = _sut.Parse("Active", "true", new CheckboxPropertySchema());
        var cb = Assert.IsType<CheckboxPropertyValue>(result);
        Assert.True(cb.Checkbox);
    }

    [Fact]
    public void Checkbox_Yes_ReturnsTrue()
    {
        var result = _sut.Parse("Active", "yes", new CheckboxPropertySchema());
        var cb = Assert.IsType<CheckboxPropertyValue>(result);
        Assert.True(cb.Checkbox);
    }

    [Fact]
    public void Checkbox_False_ReturnsFalse()
    {
        var result = _sut.Parse("Active", "false", new CheckboxPropertySchema());
        var cb = Assert.IsType<CheckboxPropertyValue>(result);
        Assert.False(cb.Checkbox);
    }

    [Fact]
    public void Checkbox_No_ReturnsFalse()
    {
        var result = _sut.Parse("Active", "no", new CheckboxPropertySchema());
        var cb = Assert.IsType<CheckboxPropertyValue>(result);
        Assert.False(cb.Checkbox);
    }

    [Fact]
    public void Date_ISO8601_ReturnsDateValue()
    {
        var result = _sut.Parse("Due", "2025-06-15", new DatePropertySchema());
        var date = Assert.IsType<DatePropertyValue>(result);
        Assert.Equal("2025-06-15", date.Date!.Start);
    }

    [Fact]
    public void Url_ReturnsUrlValue()
    {
        var result = _sut.Parse("Link", "https://example.com", new UrlPropertySchema());
        var url = Assert.IsType<UrlPropertyValue>(result);
        Assert.Equal("https://example.com", url.Url);
    }

    [Fact]
    public void UnsupportedKind_Throws()
    {
        Assert.Throws<ArgumentException>(() => _sut.Parse("Rel", "abc", new RelationPropertySchema()));
    }
}
