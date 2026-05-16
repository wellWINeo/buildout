using System.IO.Pipelines;
using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Buildin;
using Buildout.Core.DependencyInjection;
using Buildout.IntegrationTests.Buildin;
using Buildout.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Buildout.IntegrationTests.Cross;

[Collection("BuildinWireMock")]
public sealed class CreatePageIdEquivalenceTests
{
    private readonly BuildinWireMockFixture _fixture;

    private const string ParentId = "00000000-0000-0000-0000-000000000001";
    private const string NewPageId = "00000000-0000-0000-0000-000000000002";
    private const string Markdown = "# Test Page\n\nSome content.";

    public CreatePageIdEquivalenceTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private void SetupWireMockStubs()
    {
        BuildinStubs.RegisterPageProbe(_fixture.Server, ParentId, new
        {
            id = ParentId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{ParentId}",
            properties = new { title = new { type = "title", title = Array.Empty<object>() } }
        });

        BuildinStubs.RegisterCreatePage(_fixture.Server, new
        {
            id = NewPageId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{NewPageId}",
            properties = new { title = new { type = "title", title = new[] { new { type = "text", plain_text = "Test Page" } } } }
        });
    }

    private static (CommandApp app, TestConsole console) CreateCliApp(IBuildinClient client)
    {
        var services = new ServiceCollection();
        services.AddBuildoutCore();
        services.AddLogging();
        services.AddSingleton(client);

        var testConsole = new TestConsole();
        services.AddSingleton<Spectre.Console.IAnsiConsole>(testConsole);

        var caps = new TerminalCapabilities(isAnsi: false, isOutputRedirected: true, noColorEnv: null);
        services.AddSingleton(caps);
        services.AddSingleton<MarkdownTerminalRenderer>();

        var pinnedTypes = new HashSet<Type> { typeof(Spectre.Console.IAnsiConsole) };
        var registrar = new TypeRegistrar(services, pinnedTypes);

        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.PropagateExceptions();
            config.AddCommand<CreateCommand>("create");
        });

        return (app, testConsole);
    }

    [Fact]
    public async Task CliPrintId_EqualsResourceLinkUri_OfMcpCreatePage()
    {
        SetupWireMockStubs();

        // --- CLI side ---
        var cliClient = _fixture.CreateClient();
        var (app, testConsole) = CreateCliApp(cliClient);

        string cliId;
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, Markdown);

            var exitCode = await app.RunAsync(["create", tempFile, "--parent", ParentId, "--print", "id"]);
            Assert.Equal(0, exitCode);

            cliId = testConsole.Output.Trim();
        }
        finally
        {
            File.Delete(tempFile);
        }

        // --- MCP side ---
        var mcpClient = _fixture.CreateClient();
        var services = new ServiceCollection();
        services.AddBuildoutCore();
        services.AddSingleton(mcpClient);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddMcpServer().WithTools<CreatePageToolHandler>();

        await using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var c2s = new Pipe();
        var s2c = new Pipe();

        var mcpServer = McpServer.Create(
            new StreamServerTransport(c2s.Reader.AsStream(), s2c.Writer.AsStream()),
            options,
            sp.GetRequiredService<ILoggerFactory>(),
            sp);

        _ = mcpServer.RunAsync();

        var mcpMcpClient = await McpClient.CreateAsync(
            new StreamClientTransport(c2s.Writer.AsStream(), s2c.Reader.AsStream()),
            new McpClientOptions(),
            sp.GetRequiredService<ILoggerFactory>());

        try
        {
            var result = await mcpMcpClient.CallToolAsync("create_page", new Dictionary<string, object?>
            {
                ["parent_id"] = ParentId,
                ["markdown"] = Markdown,
                ["title"] = "Test Page",
            });

            var link = Assert.IsType<ResourceLinkBlock>(Assert.Single(result.Content));
            Assert.StartsWith("buildin://", link.Uri);
            var mcpId = link.Uri["buildin://".Length..];

            Assert.NotEmpty(cliId);
            Assert.Equal(NewPageId, cliId);
            Assert.Equal(cliId, mcpId);
        }
        finally
        {
            await mcpMcpClient.DisposeAsync();
            await mcpServer.DisposeAsync();
            c2s.Writer.Complete();
            c2s.Reader.Complete();
            s2c.Writer.Complete();
            s2c.Reader.Complete();
        }
    }

    private sealed class TypeRegistrar : ITypeRegistrar
    {
        private readonly IServiceCollection _services;
        private readonly HashSet<Type> _pinnedTypes;

        public TypeRegistrar(IServiceCollection services, HashSet<Type>? pinnedTypes = null)
        {
            _services = services;
            _pinnedTypes = pinnedTypes ?? [];
        }

        public void Register(Type service, Type implementation) => _services.AddSingleton(service, implementation);
        public void RegisterInstance(Type service, object implementation)
        {
            if (!_pinnedTypes.Contains(service))
                _services.AddSingleton(service, implementation);
        }
        public void RegisterLazy(Type service, Func<object> factory)
        {
            if (!_pinnedTypes.Contains(service))
                _services.AddSingleton(service, _ => factory());
        }
        public ITypeResolver Build() => new TypeResolver(_services.BuildServiceProvider());
    }

    private sealed class TypeResolver : ITypeResolver
    {
        private readonly IServiceProvider _provider;
        public TypeResolver(IServiceProvider provider) => _provider = provider;
        public object? Resolve(Type? type) => type is null ? null : _provider.GetService(type);
    }
}
