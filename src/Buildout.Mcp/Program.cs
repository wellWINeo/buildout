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

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithResources<PageResourceHandler>()
    .WithTools<SearchToolHandler>()
    .WithTools<DatabaseViewToolHandler>()
    .WithTools<CreatePageToolHandler>()
    .WithTools<GetPageMarkdownToolHandler>()
    .WithTools<UpdatePageToolHandler>();

await builder.Build().RunAsync();
