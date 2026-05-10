using System.Globalization;
using System.Text;
using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;

namespace Buildout.Core.DatabaseViews.Styles;

internal sealed class TimelineViewStyle : IDatabaseViewStyle
{
    public DatabaseViewStyle Key => DatabaseViewStyle.Timeline;

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

        var byMonth = new SortedDictionary<(int Year, int Month), List<(DatabaseRow Row, DateOnly Start, DateOnly? End)>>();
        var undated = new List<DatabaseRow>();

        foreach (var row in rows)
        {
            var (start, end) = ExtractDates(row, dateKey);
            if (start is null)
            {
                undated.Add(row);
                continue;
            }

            var key = (start.Value.Year, start.Value.Month);
            if (!byMonth.TryGetValue(key, out var list))
            {
                list = [];
                byMonth[key] = list;
            }
            list.Add((row, start.Value, end));
        }

        var sb = new StringBuilder();
        var first = true;

        foreach (var ((year, month), entries) in byMonth)
        {
            if (!first) sb.Append('\n');
            first = false;

            sb.Append(CultureInfo.InvariantCulture, $"## {year:D4}-{month:D2}");

            foreach (var (row, start, end) in entries)
            {
                var title = GetTitle(row, titleKey, formatter, budget);
                var dateStr = FormatEntry(start, end);
                sb.Append('\n').Append("- ").Append(title).Append(": ").Append(dateStr);
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

    private static string FormatEntry(DateOnly start, DateOnly? end)
    {
        if (end is null)
            return $"{start:yyyy-MM-dd} (1d)";

        var days = (end.Value.DayNumber - start.DayNumber) + 1;
        return $"{start:yyyy-MM-dd} → {end.Value:yyyy-MM-dd} ({days}d)";
    }

    private static (DateOnly? Start, DateOnly? End) ExtractDates(DatabaseRow row, string dateKey)
    {
        if (!row.Properties.TryGetValue(dateKey, out var value))
            return (null, null);

        if (value is not DatePropertyValue { Date: { } range })
            return (null, null);

        DateOnly? start = null;
        DateOnly? end = null;

        if (range.Start is not null)
        {
            if (DateOnly.TryParse(range.Start, out var s))
                start = s;
            else if (DateTimeOffset.TryParse(range.Start, out var dto))
                start = DateOnly.FromDateTime(dto.Date);
        }

        if (range.End is not null)
        {
            if (DateOnly.TryParse(range.End, out var e))
                end = e;
            else if (DateTimeOffset.TryParse(range.End, out var dto))
                end = DateOnly.FromDateTime(dto.Date);
        }

        return (start, end);
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
