using Buildout.Core.DependencyInjection;
using Buildout.Mcp.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBuildinClient(builder.Configuration);
builder.Services.AddBuildoutCore();

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithResources<PageResourceHandler>();

await builder.Build().RunAsync();
