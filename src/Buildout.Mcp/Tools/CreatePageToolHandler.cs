using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Buildout.Core.Diagnostics;
using Buildout.Core.Markdown.Authoring;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Buildout.Mcp.Tools;

[McpServerToolType]
public sealed class CreatePageToolHandler
{
    private readonly IPageCreator _creator;

    public CreatePageToolHandler(IPageCreator creator)
    {
        _creator = creator;
    }

    [McpServerTool(Name = "create_page")]
    [Description("Create a new buildin page from a Markdown document. Returns a resource_link pointing at buildin://<new_page_id>.")]
#pragma warning disable CA1707
    public async Task<CallToolResult> CreatePageAsync(
        [Description("Buildin page id or database id under which to create the new page.")] string parent_id,
        [Description("Markdown body of the new page. May start with a leading '# Title'.")] string markdown,
        [Description("Optional page title. Overrides leading '# Title' when set.")] string? title = null,
        [Description("Optional icon: a single emoji or an absolute URL.")] string? icon = null,
        [Description("Optional cover image URL.")] string? cover_url = null,
        [Description("Optional database-property values. Keys are property names; values are plain-string serialisations.")] IReadOnlyDictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default)
#pragma warning restore CA1707
    {
        var input = new CreatePageInput
        {
            ParentId = parent_id,
            Markdown = markdown,
            Title = title,
            Icon = icon,
            CoverUrl = cover_url,
            Properties = properties,
        };

        var sw = Stopwatch.StartNew();
        try
        {
            CreatePageOutcome outcome;
            try
            {
                outcome = await _creator.CreateAsync(input, cancellationToken);
            }
            catch (PartialCreationException ex)
            {
                throw new McpProtocolException(ex.Message, McpErrorCode.InternalError);
            }

            _ = outcome.FailureClass switch
            {
                FailureClass.Validation => throw new McpProtocolException(
                    outcome.UnderlyingException?.Message ?? "Validation error.", McpErrorCode.InvalidParams),
                FailureClass.NotFound => throw new McpProtocolException(
                    $"Parent '{parent_id}' was not found as a page or a database.", McpErrorCode.ResourceNotFound),
                FailureClass.Auth => throw new McpProtocolException(
                    $"Authentication error: {outcome.UnderlyingException?.Message}", McpErrorCode.InternalError),
                FailureClass.Transport => throw new McpProtocolException(
                    $"Transport error: {outcome.UnderlyingException?.Message}", McpErrorCode.InternalError),
                FailureClass.Unexpected => throw new McpProtocolException(
                    $"Unexpected error: {outcome.UnderlyingException?.Message}", McpErrorCode.InternalError),
                _ => (object?)null,
            };

            var resolvedTitle = outcome.ResolvedTitle ?? outcome.NewPageId;

            var result = new CallToolResult
            {
                IsError = false,
                Content =
                [
                    new ResourceLinkBlock
                    {
                        Uri = $"buildin://{outcome.NewPageId}",
                        Name = resolvedTitle,
                    },
                ],
            };

            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "create_page" }, { "outcome", "success" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "create_page" }, { "outcome", "success" } });
            return result;
        }
        catch (Exception)
        {
            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "create_page" }, { "outcome", "failure" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "create_page" }, { "outcome", "failure" } });
            throw;
        }
    }
}
