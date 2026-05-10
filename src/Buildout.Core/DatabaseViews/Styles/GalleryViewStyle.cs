using System.Text;
using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;

namespace Buildout.Core.DatabaseViews.Styles;

internal sealed class GalleryViewStyle : IDatabaseViewStyle
{
    public DatabaseViewStyle Key => DatabaseViewStyle.Gallery;

    private const int CardWidth = 19;
    private const int MaxSecondaryProps = 3;

    public string Render(
        Database database,
        IReadOnlyList<DatabaseRow> rows,
        DatabaseViewRequest request,
        IPropertyValueFormatter formatter,
        CellBudget budget)
    {
        if (rows.Count == 0)
            return "(no rows)";

        var (titleKey, secondaryKeys) = GetColumns(database);

        var sb = new StringBuilder();

        for (var i = 0; i < rows.Count; i++)
        {
            if (i > 0)
                sb.Append("\n\n");

            RenderCard(sb, rows[i], titleKey, secondaryKeys, formatter, budget);
        }

        return sb.ToString();
    }

    private static (string? TitleKey, List<string> SecondaryKeys) GetColumns(Database database)
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

        var secondary = database.Properties
            .Where(kvp => kvp.Key != titleKey && kvp.Value is not TitlePropertySchema)
            .Select(kvp => kvp.Key)
            .Take(MaxSecondaryProps)
            .ToList();

        return (titleKey, secondary);
    }

    private static void RenderCard(
        StringBuilder sb,
        DatabaseRow row,
        string? titleKey,
        List<string> secondaryKeys,
        IPropertyValueFormatter formatter,
        CellBudget budget)
    {
        var border = new string('─', CardWidth);
        sb.Append('┌').Append(border).Append('┐').Append('\n');
        sb.Append("│ ").Append("[cover: none]".PadRight(CardWidth - 1)).Append('│').Append('\n');
        sb.Append('├').Append(border).Append('┤').Append('\n');

        var title = titleKey is not null && row.Properties.TryGetValue(titleKey, out var tv)
            ? formatter.Format(tv, budget)
            : "—";

        sb.Append("│ ").Append(Pad(title, CardWidth - 1)).Append('│').Append('\n');

        foreach (var key in secondaryKeys)
        {
            var val = row.Properties.TryGetValue(key, out var pv)
                ? formatter.Format(pv, budget)
                : "—";
            var line = $"{key}: {val}";
            sb.Append("│ ").Append(Pad(line, CardWidth - 1)).Append('│').Append('\n');
        }

        sb.Append('└').Append(border).Append('┘');
    }

    private static string Pad(string value, int width)
    {
        if (value.Length >= width)
            return value[..width];
        return value.PadRight(width);
    }
}
