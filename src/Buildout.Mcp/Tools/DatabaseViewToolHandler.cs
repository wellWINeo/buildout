using System.ComponentModel;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.DatabaseViews;
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
    [Description("Render a buildin database as plain text in a chosen view style. Read-only. Follows pagination to exhaustion.")]
#pragma warning disable CA1707
    public async Task<string> RenderAsync(
        [Description("The buildin database id.")] string database_id,
        [Description("View style: table, board, gallery, list, calendar, timeline. Defaults to table.")] string? style = null,
        [Description("Property name to group by. Required when style is board.")] string? group_by = null,
        [Description("Property name carrying a date. Required when style is calendar or timeline.")] string? date_property = null,
        CancellationToken cancellationToken = default)
#pragma warning restore CA1707
    {
        try
        {
            var request = new DatabaseViewRequest(
                database_id,
                ParseStyle(style),
                group_by,
                date_property);

            return await _renderer.RenderAsync(request, cancellationToken);
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
