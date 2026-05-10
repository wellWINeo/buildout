namespace Buildout.Core.DatabaseViews.Rendering;

internal static class DatabaseViewMetadataHeader
{
    public static string Build(string databaseTitle, DatabaseViewStyle style, string? groupByProperty, string? dateProperty, bool isInline)
    {
        if (isInline)
            return $"## {databaseTitle}";

        var styleName = style.ToString().ToLowerInvariant();
        var header = $"# {databaseTitle} — {styleName} view";

        if (style == DatabaseViewStyle.Board && groupByProperty is not null)
            header += $" (grouped by {groupByProperty})";
        else if (style is DatabaseViewStyle.Calendar or DatabaseViewStyle.Timeline && dateProperty is not null)
            header += $" (by {dateProperty})";

        return header;
    }
}
