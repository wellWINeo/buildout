using System.Reflection;
using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Xunit;

namespace Buildout.UnitTests.DatabaseViews.Properties;

public sealed class PropertyValueFormatterTests
{
    private readonly PropertyValueFormatter _formatter = new();
    private readonly CellBudget _budget = new(24, "\u2026");

    [Fact]
    public void TitlePropertyValue_ConcatenatesRichTextPlain()
    {
        var value = new TitlePropertyValue
        {
            Title =
            [
                new() { Type = "text", Content = "Hello " },
                new() { Type = "text", Content = "World" }
            ]
        };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void RichTextPropertyValue_ConcatenatesRichTextPlain()
    {
        var value = new RichTextPropertyValue
        {
            RichText =
            [
                new() { Type = "text", Content = "Some " },
                new() { Type = "mention", Content = "@user" }
            ]
        };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("Some @user", result);
    }

    [Fact]
    public void NumberPropertyValue_WithValue_FormatsInvariant()
    {
        var value = new NumberPropertyValue { Number = 42.5 };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("42.5", result);
    }

    [Fact]
    public void NumberPropertyValue_Null_ReturnsEmDash()
    {
        var value = new NumberPropertyValue { Number = null };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("\u2014", result);
    }

    [Fact]
    public void SelectPropertyValue_WithOption_ReturnsName()
    {
        var value = new SelectPropertyValue
        {
            Select = new SelectOption { Id = "opt1", Name = "High" }
        };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("High", result);
    }

    [Fact]
    public void SelectPropertyValue_Null_ReturnsEmDash()
    {
        var value = new SelectPropertyValue { Select = null };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("\u2014", result);
    }

    [Fact]
    public void MultiSelectPropertyValue_WithOptions_CommaJoinsNames()
    {
        var value = new MultiSelectPropertyValue
        {
            MultiSelect =
            [
                new SelectOption { Id = "1", Name = "Red" },
                new SelectOption { Id = "2", Name = "Blue" },
                new SelectOption { Id = "3", Name = "Green" }
            ]
        };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("Red, Blue, Green", result);
    }

    [Fact]
    public void MultiSelectPropertyValue_Empty_ReturnsEmDash()
    {
        var value = new MultiSelectPropertyValue { MultiSelect = [] };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("\u2014", result);
    }

    [Fact]
    public void DatePropertyValue_StartOnly_ReturnsStart()
    {
        var value = new DatePropertyValue
        {
            Date = new DateRange { Start = "2025-01-15" }
        };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("2025-01-15", result);
    }

    [Fact]
    public void DatePropertyValue_StartAndEnd_ReturnsRange()
    {
        var value = new DatePropertyValue
        {
            Date = new DateRange { Start = "2025-01-15", End = "2025-01-20" }
        };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("2025-01-15 \u2192 2025-01-20", result);
    }

    [Fact]
    public void DatePropertyValue_Null_ReturnsEmDash()
    {
        var value = new DatePropertyValue { Date = null };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("\u2014", result);
    }

    [Fact]
    public void CheckboxPropertyValue_True_ReturnsChecked()
    {
        var value = new CheckboxPropertyValue { Checkbox = true };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("[x]", result);
    }

    [Fact]
    public void CheckboxPropertyValue_False_ReturnsUnchecked()
    {
        var value = new CheckboxPropertyValue { Checkbox = false };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("[ ]", result);
    }

    [Fact]
    public void CheckboxPropertyValue_Null_ReturnsUnchecked()
    {
        var value = new CheckboxPropertyValue { Checkbox = null };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("[ ]", result);
    }

    [Fact]
    public void UrlPropertyValue_WithValue_ReturnsUrl()
    {
        var value = new UrlPropertyValue { Url = "https://example.com" };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("https://example.com", result);
    }

    [Fact]
    public void UrlPropertyValue_Null_ReturnsEmDash()
    {
        var value = new UrlPropertyValue { Url = null };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("\u2014", result);
    }

    [Fact]
    public void PeoplePropertyValue_WithUsers_CommaJoinsNames()
    {
        var value = new PeoplePropertyValue
        {
            People =
            [
                new() { Id = "u1", Name = "Alice", Type = "person" },
                new() { Id = "u2", Name = "Bob", Type = "person" }
            ]
        };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("Alice, Bob", result);
    }

    [Fact]
    public void PeoplePropertyValue_Empty_ReturnsEmDash()
    {
        var value = new PeoplePropertyValue { People = [] };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("\u2014", result);
    }

    [Fact]
    public void FilesPropertyValue_WithFiles_ReturnsCount()
    {
        var value = new FilesPropertyValue
        {
            Files =
            [
                new() { Id = "f1", Name = "doc.pdf" },
                new() { Id = "f2", Name = "img.png" },
                new() { Id = "f3", Name = "data.csv" }
            ]
        };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("[3 files]", result);
    }

    [Fact]
    public void FilesPropertyValue_Empty_ReturnsZeroFiles()
    {
        var value = new FilesPropertyValue { Files = [] };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("[0 files]", result);
    }

    [Fact]
    public void RelationPropertyValue_WithIds_ReturnsCount()
    {
        var value = new RelationPropertyValue
        {
            RelationIds = ["id-1", "id-2", "id-3", "id-4"]
        };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("[4 related]", result);
    }

    [Fact]
    public void RelationPropertyValue_Empty_ReturnsZeroRelated()
    {
        var value = new RelationPropertyValue { RelationIds = [] };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("[0 related]", result);
    }

    [Fact]
    public void RollupPropertyValue_WithInnerValue_FormatsRecursively()
    {
        var value = new RollupPropertyValue
        {
            RollupResults =
            [
                new NumberPropertyValue { Number = 99 }
            ]
        };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("99", result);
    }

    [Fact]
    public void RollupPropertyValue_Empty_ReturnsPlaceholder()
    {
        var value = new RollupPropertyValue { RollupResults = [] };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("[rollup]", result);
    }

    [Fact]
    public void FormulaPropertyValue_StringResult_ReturnsString()
    {
        var value = new FormulaPropertyValue { StringResult = "computed" };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("computed", result);
    }

    [Fact]
    public void FormulaPropertyValue_NumberResult_FormatsInvariant()
    {
        var value = new FormulaPropertyValue { NumberResult = 3.14 };

        var result = _formatter.Format(value, _budget);

        Assert.Equal("3.14", result);
    }

    [Fact]
    public void FormulaPropertyValue_NoResult_ReturnsPlaceholder()
    {
        var value = new FormulaPropertyValue();

        var result = _formatter.Format(value, _budget);

        Assert.Equal("[formula]", result);
    }

    [Fact]
    public void LongValue_IsTruncatedByBudget()
    {
        var value = new TitlePropertyValue
        {
            Title = [new() { Type = "text", Content = "This is a very long title that exceeds the budget" }]
        };
        var budget = new CellBudget(10, "\u2026");

        var result = _formatter.Format(value, budget);

        Assert.Equal("This is a\u2026", result);
    }

    [Fact]
    public void AllConcretePropertyValueSubtypes_AreCoveredByTests()
    {
        var assembly = typeof(PropertyValue).Assembly;
        var concreteTypes = assembly.GetTypes()
            .Where(t => t.IsSealed && t.IsSubclassOf(typeof(PropertyValue)))
            .ToList();

        var testedTypes = new HashSet<Type>
        {
            typeof(TitlePropertyValue),
            typeof(RichTextPropertyValue),
            typeof(NumberPropertyValue),
            typeof(SelectPropertyValue),
            typeof(MultiSelectPropertyValue),
            typeof(DatePropertyValue),
            typeof(CheckboxPropertyValue),
            typeof(UrlPropertyValue),
            typeof(PeoplePropertyValue),
            typeof(FilesPropertyValue),
            typeof(RelationPropertyValue),
            typeof(RollupPropertyValue),
            typeof(FormulaPropertyValue)
        };

        var uncovered = concreteTypes.Where(t => !testedTypes.Contains(t)).ToList();

        Assert.Empty(uncovered);
    }
}
