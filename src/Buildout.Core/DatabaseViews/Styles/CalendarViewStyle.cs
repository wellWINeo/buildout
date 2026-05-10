using System.Globalization;
using System.Text;
using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;

namespace Buildout.Core.DatabaseViews.Styles;

internal sealed class CalendarViewStyle : IDatabaseViewStyle
{
    public DatabaseViewStyle Key => DatabaseViewStyle.Calendar;

    public string Render(
        Database database,
        IReadOnlyList<DatabaseRow> rows,
        DatabaseViewRequest request,
        IPropertyValueFormatter formatter,
        CellBudget budget)
    {
        if (rows.Count == 0)
            return "(no rows)";

        var dateKey = request.DateProperty ?? string.Empty;
        var titleKey = FindTitleKey(database);

        var dated = new SortedDictionary<DateOnly, List<DatabaseRow>>();
        var undated = new List<DatabaseRow>();

        foreach (var row in rows)
        {
            var date = ExtractDate(row, dateKey);
            if (date is null)
            {
                undated.Add(row);
                continue;
            }

            if (!dated.TryGetValue(date.Value, out var list))
            {
                list = [];
                dated[date.Value] = list;
            }
            list.Add(row);
        }

        var sb = new StringBuilder();
        var first = true;

        foreach (var (date, dateRows) in dated)
        {
            if (!first) sb.Append('\n');
            first = false;

            var dayName = date.DayOfWeek.ToString();
            sb.Append(CultureInfo.InvariantCulture, $"## {date:yyyy-MM-dd} ({dayName})");

            foreach (var row in dateRows)
            {
                var title = GetTitle(row, titleKey, formatter, budget);
                sb.Append('\n').Append("- ").Append(title);
            }
        }

        if (undated.Count > 0)
        {
            if (!first) sb.Append('\n');
            sb.Append("(undated)");
            foreach (var row in undated)
            {
                var title = GetTitle(row, titleKey, formatter, budget);
                sb.Append('\n').Append("- ").Append(title);
            }
        }

        return sb.ToString();
    }

    private static DateOnly? ExtractDate(DatabaseRow row, string dateKey)
    {
        if (!row.Properties.TryGetValue(dateKey, out var value))
            return null;

        if (value is DatePropertyValue { Date.Start: { } start })
        {
            if (DateOnly.TryParse(start, out var d))
                return d;
            if (DateTimeOffset.TryParse(start, out var dto))
                return DateOnly.FromDateTime(dto.Date);
        }

        return null;
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
}
