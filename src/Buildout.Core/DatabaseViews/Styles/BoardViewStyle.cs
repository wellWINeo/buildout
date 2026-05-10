using System.Globalization;
using System.Text;
using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;

namespace Buildout.Core.DatabaseViews.Styles;

internal sealed class BoardViewStyle : IDatabaseViewStyle
{
    public DatabaseViewStyle Key => DatabaseViewStyle.Board;

    private const int SideBySideCap = 3;

    public string Render(
        Database database,
        IReadOnlyList<DatabaseRow> rows,
        DatabaseViewRequest request,
        IPropertyValueFormatter formatter,
        CellBudget budget)
    {
        if (rows.Count == 0)
            return "(no rows)";

        var titleKey = FindTitleKey(database);
        var groupByKey = request.GroupByProperty ?? string.Empty;
        var groupSchema = database.Properties?.GetValueOrDefault(groupByKey);

        var groups = BuildGroups(rows, groupByKey, groupSchema, formatter, budget);

        var nonEmpty = groups.Where(g => g.Key != "(none)" && g.Value.Count > 0).ToList();
        var noneGroup = groups.TryGetValue("(none)", out var noneRows) && noneRows.Count > 0
            ? noneRows
            : null;

        return nonEmpty.Count <= SideBySideCap
            ? RenderSideBySide(nonEmpty, noneGroup, titleKey, formatter, budget)
            : RenderStacked(groups, titleKey, formatter, budget);
    }

    private static Dictionary<string, List<DatabaseRow>> BuildGroups(
        IReadOnlyList<DatabaseRow> rows,
        string groupByKey,
        PropertySchema? schema,
        IPropertyValueFormatter formatter,
        CellBudget budget)
    {
        var groups = new Dictionary<string, List<DatabaseRow>>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var key = ResolveGroupKey(row, groupByKey, schema, formatter, budget);

            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }
            list.Add(row);
        }

        return groups;
    }

    private static string ResolveGroupKey(
        DatabaseRow row,
        string groupByKey,
        PropertySchema? schema,
        IPropertyValueFormatter formatter,
        CellBudget budget)
    {
        if (!row.Properties.TryGetValue(groupByKey, out var value))
            return "(none)";

        return schema switch
        {
            CheckboxPropertySchema => value is CheckboxPropertyValue { Checkbox: true } ? "Checked" : "Unchecked",
            _ => value switch
            {
                SelectPropertyValue { Select: null } => "(none)",
                MultiSelectPropertyValue { MultiSelect: null or { Count: 0 } } => "(none)",
                _ => formatter.Format(value, budget) is { } formatted && formatted == "—" ? "(none)" : formatter.Format(value, budget)
            }
        };
    }

    private static string? FindTitleKey(Database database)
    {
        if (database.Properties is null) return null;
        foreach (var kvp in database.Properties)
        {
            if (kvp.Value is TitlePropertySchema)
                return kvp.Key;
        }
        return null;
    }

    private static string GetTitle(DatabaseRow row, string? titleKey, IPropertyValueFormatter formatter, CellBudget budget)
    {
        if (titleKey is null || !row.Properties.TryGetValue(titleKey, out var v))
            return "—";
        return formatter.Format(v, budget);
    }

    private static string RenderSideBySide(
        List<KeyValuePair<string, List<DatabaseRow>>> nonEmpty,
        List<DatabaseRow>? noneGroup,
        string? titleKey,
        IPropertyValueFormatter formatter,
        CellBudget budget)
    {
        var allGroups = new List<KeyValuePair<string, List<DatabaseRow>>>(nonEmpty);
        if (noneGroup is { Count: > 0 })
            allGroups.Add(new KeyValuePair<string, List<DatabaseRow>>("(none)", noneGroup));

        if (allGroups.Count == 0)
            return "(no rows)";

        var colWidth = allGroups
            .SelectMany(g => g.Value.Select(r => GetTitle(r, titleKey, formatter, budget).Length).Append(g.Key.Length))
            .Max() + 2;

        var sb = new StringBuilder();

        var headerLine = string.Join("  ", allGroups.Select(g => g.Key.PadRight(colWidth)));
        sb.Append(headerLine.TrimEnd());

        var separator = string.Join("  ", allGroups.Select(_ => new string('-', colWidth)));
        sb.Append('\n').Append(separator.TrimEnd());

        var maxRows = allGroups.Max(g => g.Value.Count);
        for (var i = 0; i < maxRows; i++)
        {
            sb.Append('\n');
            var cells = allGroups.Select(g =>
                i < g.Value.Count
                    ? GetTitle(g.Value[i], titleKey, formatter, budget).PadRight(colWidth)
                    : new string(' ', colWidth));
            sb.Append(string.Join("  ", cells).TrimEnd());
        }

        return sb.ToString();
    }

    private static string RenderStacked(
        Dictionary<string, List<DatabaseRow>> groups,
        string? titleKey,
        IPropertyValueFormatter formatter,
        CellBudget budget)
    {
        var sb = new StringBuilder();
        var first = true;

        foreach (var (groupName, rows) in groups.Where(g => g.Key != "(none)"))
        {
            if (!first) sb.Append('\n');
            first = false;

            var label = rows.Count == 1 ? "row" : "rows";
            sb.Append(CultureInfo.InvariantCulture, $"### {groupName} ({rows.Count} {label})");

            foreach (var row in rows)
                sb.Append('\n').Append("- ").Append(GetTitle(row, titleKey, formatter, budget));
        }

        if (groups.TryGetValue("(none)", out var noneRows) && noneRows.Count > 0)
        {
            if (!first) sb.Append('\n');
            var label = noneRows.Count == 1 ? "row" : "rows";
            sb.Append(CultureInfo.InvariantCulture, $"### (none) ({noneRows.Count} {label})");
            foreach (var row in noneRows)
                sb.Append('\n').Append("- ").Append(GetTitle(row, titleKey, formatter, budget));
        }

        return sb.ToString();
    }
}
