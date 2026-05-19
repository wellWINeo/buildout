using Buildout.Core.DependencyInjection;
using Buildout.Mcp.Resources;
using Buildout.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBuildinClient(builder.Configuration);
builder.Services.AddBuildoutCore();

var telemetryEnabled = builder.Configuration["BUILDOUT_TELEMETRY_ENABLED"] is "true" or "1";
if (telemetryEnabled)
{
    var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4318";

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("buildout-mcp"))
        .WithMetrics(m => m
            .AddMeter("Buildout")
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint + "/v1/metrics")))
        .WithLogging(l => l
            .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint + "/v1/logs")));
}

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInstructions =
            """
            You are connected to a Buildin workspace via MCP. Always call the available tools
            and resources when asked to search, read, or query pages and databases — never
            answer from prior knowledge or claim you lack access.

            Buildin page URLs follow the format https://buildin.ai/<uuid>. To read a page
            from a URL, extract the UUID segment and pass it to read_buildin_page.

            Typical workflow: call search to find pages by keyword, then call
            read_buildin_page with a returned page_id to fetch full content.
            """;
    })
    .WithStdioServerTransport()
    .WithResources<PageResourceHandler>()
    .WithTools<SearchToolHandler>()
    .WithTools<DatabaseViewToolHandler>()
    .WithTools<CreatePageToolHandler>()
    .WithTools<GetPageMarkdownToolHandler>()
    .WithTools<UpdatePageToolHandler>();

await builder.Build().RunAsync();
