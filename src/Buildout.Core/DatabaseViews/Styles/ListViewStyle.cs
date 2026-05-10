using System.Text;
using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;

namespace Buildout.Core.DatabaseViews.Styles;

internal sealed class ListViewStyle : IDatabaseViewStyle
{
    public DatabaseViewStyle Key => DatabaseViewStyle.List;

    public string Render(
        Database database,
        IReadOnlyList<DatabaseRow> rows,
        DatabaseViewRequest request,
        IPropertyValueFormatter formatter,
        CellBudget budget)
    {
        if (rows.Count == 0)
            return "(no rows)";

        var (titleKey, nonTitleKeys) = GetColumns(database);

        var sb = new StringBuilder();

        for (var i = 0; i < rows.Count; i++)
        {
            if (i > 0)
                sb.Append('\n');

            var row = rows[i];
            var title = titleKey is not null && row.Properties.TryGetValue(titleKey, out var tv)
                ? formatter.Format(tv, budget)
                : "—";

            sb.Append("- ").Append(title);

            if (nonTitleKeys.Count > 0)
            {
                var props = nonTitleKeys.Select(k =>
                {
                    var val = row.Properties.TryGetValue(k, out var v)
                        ? formatter.Format(v, budget)
                        : "—";
                    return $"{k}: {val}";
                });
                sb.Append(" (").Append(string.Join(", ", props)).Append(')');
            }
        }

        return sb.ToString();
    }

    private static (string? TitleKey, List<string> NonTitleKeys) GetColumns(Database database)
    {
        if (database.Properties is null)
            return (null, []);

        string? titleKey = null;
        foreach (var kvp in database.Properties)
        {
            if (kvp.Value is TitlePropertySchema)
            {
                titleKey = kvp.Key;
                break;
            }
        }

        var nonTitle = database.Properties
            .Where(kvp => kvp.Value is not TitlePropertySchema)
            .Select(kvp => kvp.Key)
            .ToList();

        return (titleKey, nonTitle);
    }
}
