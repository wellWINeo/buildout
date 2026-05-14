using Buildout.Core.DependencyInjection;
using Buildout.Mcp.Resources;
using Buildout.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBuildinClient(builder.Configuration);
builder.Services.AddBuildoutCore();

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithResources<PageResourceHandler>()
    .WithTools<SearchToolHandler>()
    .WithTools<DatabaseViewToolHandler>()
    .WithTools<CreatePageToolHandler>();

await builder.Build().RunAsync();
