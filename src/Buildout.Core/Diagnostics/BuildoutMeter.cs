using System.Diagnostics.Metrics;

namespace Buildout.Core.Diagnostics;

public static class BuildoutMeter
{
    public static readonly Meter Meter = new("Buildout", "1.0.0");

    public static readonly Counter<long> OperationsTotal = Meter.CreateCounter<long>(
        "buildout.operations.total", "{operation}", "Total buildout operations");

    public static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "buildout.operation.duration", "s", "Operation duration");

    public static readonly Counter<long> ApiCallsTotal = Meter.CreateCounter<long>(
        "buildout.api.calls.total", "{call}", "Total buildin API calls");

    public static readonly Histogram<double> ApiCallDuration = Meter.CreateHistogram<double>(
        "buildout.api.call.duration", "s", "Duration of buildin API calls");

    public static readonly Counter<long> BlocksProcessedTotal = Meter.CreateCounter<long>(
        "buildout.blocks.processed.total", "{block}", "Total blocks read or written");

    public static readonly Counter<long> SearchResultsTotal = Meter.CreateCounter<long>(
        "buildout.search.results.total", "{result}", "Total search results");

    public static readonly Counter<long> PagesCreatedTotal = Meter.CreateCounter<long>(
        "buildout.pages.created.total", "{page}", "Total pages created");

    public static readonly Counter<long> DatabaseViewRendersTotal = Meter.CreateCounter<long>(
        "buildout.database.view.renders.total", "{render}", "Total database view renders");

    public static readonly Counter<long> McpToolInvocationsTotal = Meter.CreateCounter<long>(
        "buildout.mcp.tool.invocations.total", "{invocation}", "Total MCP tool invocations");

    public static readonly Histogram<double> McpToolDuration = Meter.CreateHistogram<double>(
        "buildout.mcp.tool.duration", "s", "Duration of MCP tool invocations");

    public static readonly Counter<long> McpResourceReadsTotal = Meter.CreateCounter<long>(
        "buildout.mcp.resource.reads.total", "{read}", "Total MCP resource reads");
}
