using Buildout.Configuration;
using Buildout.Core.DependencyInjection;
using Buildout.Mcp.Audit;
using Buildout.Mcp.Prompts;
using Buildout.Mcp.Resources;
using Buildout.Mcp.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

// Extract --config/-c before the host builder runs.
string? configPath = null;
for (var i = 0; i < args.Length; i++)
{
    if ((args[i] is "--config" or "-c") && i + 1 < args.Length)
        configPath = args[++i];
    else if (args[i].StartsWith("--config=", StringComparison.Ordinal))
        configPath = args[i]["--config=".Length..];
    else if (args[i].StartsWith("-c=", StringComparison.Ordinal))
        configPath = args[i]["-c=".Length..];
}

try
{
    var config = BuildoutConfiguration.Build(configPath);

    var builder = Host.CreateApplicationBuilder([]);
    builder.Configuration.Sources.Clear();

    var configBuilder = new ConfigurationBuilder();
    configBuilder.AddConfiguration(config);
    var mergedConfig = configBuilder.Build();

    builder.Services.AddBuildinClient(mergedConfig);
    builder.Services.AddBuildoutCore(mergedConfig);

    var telemetryEnabled = mergedConfig.GetValue<bool>("Telemetry:Enabled");
    if (telemetryEnabled)
    {
        var otlpEndpoint = mergedConfig.GetValue<Uri>("Telemetry:OtlpEndpoint") ?? new Uri("http://localhost:4318");

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

    var transportType = mergedConfig.GetValue<string>("Transport:Type") ?? "stdio";
    var isHttpTransport = transportType == "http";

    builder.Services.AddAuditTrail(mergedConfig, isHttpTransport);
    
    var mcpBuilder = builder.Services
        .AddMcpServer(options =>
        {
            options.ServerInstructions = PromptResourceLoader.Load("server-instructions");
        });

    if (isHttpTransport)
    {
        mcpBuilder = mcpBuilder.WithHttpTransport();
    }
    else
    {
        mcpBuilder = mcpBuilder.WithStdioServerTransport();
    }

    mcpBuilder
        .WithResources<PageResourceHandler>()
        .WithPrompts<BuildoutPrompts>()
        .WithTools<SearchToolHandler>()
        .WithTools<DatabaseViewToolHandler>()
        .WithTools<CreatePageToolHandler>()
        .WithTools<GetPageMarkdownToolHandler>()
        .WithTools<UpdatePageToolHandler>()
        .WithTools<DeletePageToolHandler>()
        .WithTools<RestorePageToolHandler>();

    await builder.Build().RunAsync();
}
catch (BuildoutConfigurationException ex)
{
    await Console.Error.WriteLineAsync(ex.Message);
    Environment.Exit(1);
}
