using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.DatabaseViews;
using Buildout.Core.Diagnostics;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Buildout.Mcp.Tools;

[McpServerToolType]
public sealed class DatabaseViewToolHandler
{
    private readonly IDatabaseViewRenderer _renderer;

    public DatabaseViewToolHandler(IDatabaseViewRenderer renderer)
        => _renderer = renderer;

    [McpServerTool(Name = "database_view")]
    [Description("Retrieve all records from a Buildin database and return them as plain text. Call this function with the database UUID to fetch its contents. Follows pagination automatically.")]
#pragma warning disable CA1707
    public async Task<string> RenderAsync(
        [Description("UUID of the Buildin database to retrieve.")] string database_id,
        [Description("Output format: table, board, gallery, list, calendar, or timeline. Defaults to table.")] string? style = null,
        [Description("Property name to group records by. Required when format is board.")] string? group_by = null,
        [Description("Name of the date property. Required when format is calendar or timeline.")] string? date_property = null,
        CancellationToken cancellationToken = default)
#pragma warning restore CA1707
    {
        var sw = Stopwatch.StartNew();
        try
        {
            string result;
            try
            {
                var request = new DatabaseViewRequest(
                    database_id,
                    ParseStyle(style),
                    group_by,
                    date_property);

                result = await _renderer.RenderAsync(request, cancellationToken);
            }
            catch (DatabaseViewValidationException ex)
            {
                throw new McpProtocolException(ex.Message, McpErrorCode.InvalidParams);
            }
            catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
            {
                throw new McpProtocolException($"Database not found: {database_id}", McpErrorCode.ResourceNotFound);
            }
            catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 401 or 403 })
            {
                throw new McpProtocolException($"Authentication error: {ex.Message}", McpErrorCode.InternalError);
            }
            catch (BuildinApiException ex) when (ex.Error is TransportError)
            {
                throw new McpProtocolException($"Transport error: {ex.Message}", McpErrorCode.InternalError);
            }
            catch (BuildinApiException ex)
            {
                throw new McpProtocolException($"Unexpected buildin error: {ex.Message}", McpErrorCode.InternalError);
            }

            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "database_view" }, { "outcome", "success" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "database_view" }, { "outcome", "success" } });
            return result;
        }
        catch (Exception)
        {
            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "database_view" }, { "outcome", "failure" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "database_view" }, { "outcome", "failure" } });
            throw;
        }
    }

    private static DatabaseViewStyle ParseStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
            return DatabaseViewStyle.Table;

        if (Enum.TryParse<DatabaseViewStyle>(style, ignoreCase: true, out var result))
            return result;

        throw new DatabaseViewValidationException(
            $"Unknown style '{style}'. Valid styles: {string.Join(", ", Enum.GetNames<DatabaseViewStyle>().Select(n => n.ToLowerInvariant()))}.",
            nameof(style),
            Enum.GetNames<DatabaseViewStyle>().Select(n => n.ToLowerInvariant()).ToList());
    }
}
