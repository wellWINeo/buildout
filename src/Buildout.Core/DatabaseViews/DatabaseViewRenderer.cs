using System.Globalization;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;

namespace Buildout.Core.DatabaseViews;

internal sealed class DatabaseViewRenderer : IDatabaseViewRenderer
{
    private readonly IBuildinClient _client;
    private readonly IPropertyValueFormatter _formatter;
    private readonly IReadOnlyDictionary<DatabaseViewStyle, IDatabaseViewStyle> _styles;
    private readonly CellBudget _budget;

    public DatabaseViewRenderer(
        IBuildinClient client,
        IPropertyValueFormatter formatter,
        IReadOnlyDictionary<DatabaseViewStyle, IDatabaseViewStyle> styles,
        CellBudget budget)
    {
        _client = client;
        _formatter = formatter;
        _styles = styles;
        _budget = budget;
    }

    public async Task<string> RenderAsync(DatabaseViewRequest request, CancellationToken cancellationToken = default)
    {
        ValidateStatic(request);

        var database = await _client.GetDatabaseAsync(request.DatabaseId, cancellationToken).ConfigureAwait(false);

        ValidateSchema(request, database);

        var rows = await PaginateRowsAsync(request.DatabaseId, cancellationToken).ConfigureAwait(false);

        if (!_styles.TryGetValue(request.Style, out var style))
            throw new DatabaseViewValidationException(
                $"Unknown style '{request.Style}'.",
                nameof(request.Style),
                Enum.GetNames<DatabaseViewStyle>());

        var rendered = style.Render(database, rows, request, _formatter, _budget);

        var title = ExtractTitle(database);
        var header = DatabaseViewMetadataHeader.Build(title, request.Style, request.GroupByProperty, request.DateProperty, isInline: false);

        return $"{header}\n\n{rendered}";
    }

    public async Task<string> RenderInlineAsync(string databaseId, CancellationToken cancellationToken = default)
    {
        var database = await _client.GetDatabaseAsync(databaseId, cancellationToken).ConfigureAwait(false);
        var rows = await PaginateRowsAsync(databaseId, cancellationToken).ConfigureAwait(false);

        if (!_styles.TryGetValue(DatabaseViewStyle.Table, out var style))
            throw new DatabaseViewValidationException(
                "Table style not registered.",
                nameof(DatabaseViewStyle.Table),
                Array.Empty<string>());

        var rendered = style.Render(database, rows, new DatabaseViewRequest(databaseId, DatabaseViewStyle.Table, null, null), _formatter, _budget);

        var title = ExtractTitle(database);
        var header = DatabaseViewMetadataHeader.Build(title, DatabaseViewStyle.Table, null, null, isInline: true);

        return $"{header}\n\n{rendered}";
    }

    private static void ValidateStatic(DatabaseViewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DatabaseId))
            throw new DatabaseViewValidationException(
                "Database id is required.",
                nameof(request.DatabaseId),
                Array.Empty<string>());

        if (request.Style == DatabaseViewStyle.Board && string.IsNullOrWhiteSpace(request.GroupByProperty))
            throw new DatabaseViewValidationException(
                "Board view requires --group-by.",
                nameof(request.GroupByProperty),
                Array.Empty<string>());

        if ((request.Style == DatabaseViewStyle.Calendar || request.Style == DatabaseViewStyle.Timeline)
            && string.IsNullOrWhiteSpace(request.DateProperty))
            throw new DatabaseViewValidationException(
                $"{request.Style} view requires --date-property.",
                nameof(request.DateProperty),
                Array.Empty<string>());
    }

    private static void ValidateSchema(DatabaseViewRequest request, Database database)
    {
        if (database.Properties is null) return;

        if (request.Style == DatabaseViewStyle.Board && !string.IsNullOrWhiteSpace(request.GroupByProperty))
        {
            if (!database.Properties.TryGetValue(request.GroupByProperty, out var schema) ||
                schema is not (SelectPropertySchema or MultiSelectPropertySchema or CheckboxPropertySchema))
            {
                var valid = database.Properties
                    .Where(kv => kv.Value is SelectPropertySchema or MultiSelectPropertySchema or CheckboxPropertySchema)
                    .Select(kv => kv.Key)
                    .ToList();

                throw new DatabaseViewValidationException(
                    $"Unknown or invalid group-by property '{request.GroupByProperty}'.",
                    nameof(request.GroupByProperty),
                    valid);
            }
        }

        if ((request.Style == DatabaseViewStyle.Calendar || request.Style == DatabaseViewStyle.Timeline)
            && !string.IsNullOrWhiteSpace(request.DateProperty))
        {
            if (!database.Properties.TryGetValue(request.DateProperty, out var schema) ||
                schema is not (DatePropertySchema or CreatedTimePropertySchema))
            {
                var valid = database.Properties
                    .Where(kv => kv.Value is DatePropertySchema or CreatedTimePropertySchema)
                    .Select(kv => kv.Key)
                    .ToList();

                throw new DatabaseViewValidationException(
                    $"Unknown or invalid date property '{request.DateProperty}'.",
                    nameof(request.DateProperty),
                    valid);
            }
        }
    }

    private async Task<List<DatabaseRow>> PaginateRowsAsync(string databaseId, CancellationToken ct)
    {
        var rows = new List<DatabaseRow>();
        string? cursor = null;

        do
        {
            var queryRequest = new QueryDatabaseRequest { StartCursor = cursor };
            var result = await _client.QueryDatabaseAsync(databaseId, queryRequest, ct).ConfigureAwait(false);

            foreach (var props in result.Results)
                rows.Add(new DatabaseRow(string.Empty, props));

            cursor = result.HasMore ? result.NextCursor : null;
        }
        while (cursor is not null);

        return rows;
    }

    private static string ExtractTitle(Database database)
    {
        if (database.Title is null or { Count: 0 })
            return "(untitled)";

        return string.Concat(database.Title.Select(rt => rt.Content));
    }
}
