using System.Globalization;
using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews.Rendering;

namespace Buildout.Core.DatabaseViews.Properties;

internal sealed class PropertyValueFormatter : IPropertyValueFormatter
{
    public string Format(PropertyValue value, CellBudget budget) => value switch
    {
        TitlePropertyValue t => budget.Truncate(ExtractPlainText(t.Title)),
        RichTextPropertyValue rt => budget.Truncate(ExtractPlainText(rt.RichText)),
        NumberPropertyValue n => n.Number?.ToString(CultureInfo.InvariantCulture) ?? "\u2014",
        SelectPropertyValue s => s.Select?.Name ?? "\u2014",
        MultiSelectPropertyValue ms when ms.MultiSelect is { Count: > 0 }
            => budget.Truncate(string.Join(", ", ms.MultiSelect.Select(o => o.Name))),
        MultiSelectPropertyValue => "\u2014",
        DatePropertyValue d when d.Date is null => "\u2014",
        DatePropertyValue { Date.End: not null } d
            => $"{d.Date!.Start} \u2192 {d.Date.End}",
        DatePropertyValue d => d.Date!.Start ?? "\u2014",
        CheckboxPropertyValue c => c.Checkbox is true ? "[x]" : "[ ]",
        UrlPropertyValue u => u.Url ?? "\u2014",
        PeoplePropertyValue p when p.People is { Count: > 0 }
            => budget.Truncate(string.Join(", ", p.People.Select(u => u.Name))),
        PeoplePropertyValue => "\u2014",
        FilesPropertyValue f => f.Files is { Count: > 0 }
            ? $"[{f.Files.Count} files]"
            : "[0 files]",
        RelationPropertyValue r => r.RelationIds is { Count: > 0 }
            ? $"[{r.RelationIds.Count} related]"
            : "[0 related]",
        RollupPropertyValue ru when ru.RollupResults is { Count: 1 }
            => Format(ru.RollupResults[0], budget),
        RollupPropertyValue => "[rollup]",
        FormulaPropertyValue f when f.StringResult is not null
            => budget.Truncate(f.StringResult),
        FormulaPropertyValue f when f.NumberResult is not null
            => f.NumberResult.Value.ToString(CultureInfo.InvariantCulture),
        FormulaPropertyValue => "[formula]",
        _ => "[unsupported]"
    };

    private static string ExtractPlainText(IReadOnlyList<RichText>? segments)
    {
        if (segments is null or { Count: 0 })
            return "\u2014";

        return string.Concat(segments.Select(s => s.Content));
    }
}
