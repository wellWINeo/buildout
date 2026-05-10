using System.Text;
using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;

namespace Buildout.Core.DatabaseViews.Styles;

internal sealed class TableViewStyle : IDatabaseViewStyle
{
    public DatabaseViewStyle Key => DatabaseViewStyle.Table;

    private const int MaxColumnsForTable = 6;

    public string Render(
        Database database,
        IReadOnlyList<DatabaseRow> rows,
        DatabaseViewRequest request,
        IPropertyValueFormatter formatter,
        CellBudget budget)
    {
        if (rows.Count == 0)
            return "(no rows)";

        var columns = GetColumns(database);

        return columns.Count > MaxColumnsForTable
            ? RenderStacked(columns, rows, formatter, budget)
            : RenderTable(columns, rows, formatter, budget);
    }

    private static List<(string Name, PropertySchema Schema)> GetColumns(Database database)
    {
        var columns = new List<(string Name, PropertySchema Schema)>();
        string? titleKey = null;

        if (database.Properties is not null)
        {
            foreach (var kvp in database.Properties)
            {
                if (kvp.Value is TitlePropertySchema)
                {
                    titleKey = kvp.Key;
                    columns.Add((kvp.Key, kvp.Value));
                    break;
                }
            }

            foreach (var kvp in database.Properties)
            {
                if (kvp.Key != titleKey)
                    columns.Add((kvp.Key, kvp.Value));
            }
        }

        return columns;
    }

    private static string RenderTable(
        List<(string Name, PropertySchema Schema)> columns,
        IReadOnlyList<DatabaseRow> rows,
        IPropertyValueFormatter formatter,
        CellBudget budget)
    {
        var sb = new StringBuilder();

        sb.Append("| ").Append(string.Join(" | ", columns.Select(c => c.Name))).Append(" |\n");
        sb.Append("| ").Append(string.Join(" | ", columns.Select(_ => "---"))).Append(" |");

        foreach (var row in rows)
        {
            var cells = columns.Select(c =>
                row.Properties.TryGetValue(c.Name, out var v)
                    ? formatter.Format(v, budget)
                    : "\u2014");
            sb.Append('\n').Append("| ").Append(string.Join(" | ", cells)).Append(" |");
        }

        return sb.ToString();
    }

    private static string RenderStacked(
        List<(string Name, PropertySchema Schema)> columns,
        IReadOnlyList<DatabaseRow> rows,
        IPropertyValueFormatter formatter,
        CellBudget budget)
    {
        var sb = new StringBuilder();
        var titleColumn = columns.FirstOrDefault(c => c.Schema is TitlePropertySchema);
        var otherColumns = columns.Where(c => c.Schema is not TitlePropertySchema).ToList();

        for (var i = 0; i < rows.Count; i++)
        {
            if (i > 0)
                sb.Append('\n');

            var row = rows[i];
            var titleValue = titleColumn.Name is not null &&
                             row.Properties.TryGetValue(titleColumn.Name, out var tv)
                ? formatter.Format(tv, budget)
                : "\u2014";

            sb.Append("─ ").Append(titleValue);

            foreach (var col in otherColumns)
            {
                var value = row.Properties.TryGetValue(col.Name, out var v) ? v : null;
                var formatted = value is null ? "\u2014" : formatter.Format(value, budget);
                sb.Append('\n').Append("    ").Append(col.Name).Append(": ").Append(formatted);
            }
        }

        return sb.ToString();
    }
}
