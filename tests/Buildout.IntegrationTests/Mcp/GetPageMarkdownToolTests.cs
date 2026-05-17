using System.IO.Pipelines;
using System.Text.Json;
using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Markdown;
using Buildout.Core.Markdown.Editing;
using Buildout.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Spectre.Console.Cli;
using Xunit;

namespace Buildout.IntegrationTests.Mcp;

public sealed class GetPageMarkdownToolTests : IAsyncLifetime
{
    private readonly IPageEditor _editor = Substitute.For<IPageEditor>();
    private ServiceProvider _sp = null!;
    private McpServer _server = null!;
    private McpClient _client = null!;
    private Pipe _c2s = null!;
    private Pipe _s2c = null!;

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPageEditor>(_editor);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.AddMcpServer().WithTools<GetPageMarkdownToolHandler>();

        _sp = services.BuildServiceProvider();

        var options = _sp.GetRequiredService<IOptions<McpServerOptions>>().Value;

        _c2s = new Pipe();
        _s2c = new Pipe();

        _server = McpServer.Create(
            new StreamServerTransport(
                _c2s.Reader.AsStream(),
                _s2c.Writer.AsStream()),
            options,
            _sp.GetRequiredService<ILoggerFactory>(),
            _sp);

        _ = _server.RunAsync();

        _client = await McpClient.CreateAsync(
            new StreamClientTransport(
                _c2s.Writer.AsStream(),
                _s2c.Reader.AsStream()),
            new McpClientOptions(),
            _sp.GetRequiredService<ILoggerFactory>());
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        await _server.DisposeAsync();
        _c2s.Writer.Complete();
        _c2s.Reader.Complete();
        _s2c.Writer.Complete();
        _s2c.Reader.Complete();
        await _sp.DisposeAsync();
    }

    [Fact]
    public async Task ServerAdvertisesGetPageMarkdownTool()
    {
        var tools = await _client.ListToolsAsync();

        Assert.Single(tools);
        Assert.Equal("get_page_markdown", tools[0].Name);
        Assert.NotNull(tools[0].Description);
    }

    [Fact]
    public async Task HappyPath_ReturnsStructuredTriple()
    {
        var snapshot = new AnchoredPageSnapshot
        {
            Markdown = "# Title\n\nSome content.",
            Revision = "abcd1234",
            UnknownBlockIds = [],
        };

        _editor.FetchForEditAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(snapshot);

        var result = await _client.CallToolAsync("get_page_markdown", new Dictionary<string, object?>
        {
            ["page_id"] = "page-1",
        });

        var text = result.Content.OfType<TextContentBlock>().First().Text;
        var doc = JsonDocument.Parse(text);

        Assert.True(doc.RootElement.TryGetProperty("Markdown", out var mdProp));
        Assert.True(doc.RootElement.TryGetProperty("Revision", out var revProp));
        Assert.True(doc.RootElement.TryGetProperty("UnknownBlockIds", out var ubiProp));

        Assert.Equal("# Title\n\nSome content.", mdProp.GetString());
        Assert.Equal("abcd1234", revProp.GetString());
        Assert.Equal(JsonValueKind.Array, ubiProp.ValueKind);
        Assert.Empty(ubiProp.EnumerateArray());
    }

    [Fact]
    public async Task UnsupportedBlocks_PopulatesUnknownBlockIds()
    {
        var snapshot = new AnchoredPageSnapshot
        {
            Markdown = "# Title\n\nContent.",
            Revision = "deadbeef",
            UnknownBlockIds = ["child-page-aaa", "child-database-bbb"],
        };

        _editor.FetchForEditAsync("page-2", Arg.Any<CancellationToken>())
            .Returns(snapshot);

        var result = await _client.CallToolAsync("get_page_markdown", new Dictionary<string, object?>
        {
            ["page_id"] = "page-2",
        });

        var text = result.Content.OfType<TextContentBlock>().First().Text;
        var doc = JsonDocument.Parse(text);

        var ubi = doc.RootElement.GetProperty("UnknownBlockIds");
        var ids = ubi.EnumerateArray().Select(e => e.GetString()!).ToList();

        Assert.Equal(2, ids.Count);
        Assert.Contains("child-page-aaa", ids);
        Assert.Contains("child-database-bbb", ids);
    }

    [Fact]
    public async Task Revision_MatchesCliEditingCall()
    {
        const string PageId = "page-rev-match";
        const string Markdown = "# Hello\n\nWorld.";
        const string Revision = "a1b2c3d4";

        var snapshot = new AnchoredPageSnapshot
        {
            Markdown = Markdown,
            Revision = Revision,
            UnknownBlockIds = [],
        };

        _editor.FetchForEditAsync(PageId, Arg.Any<CancellationToken>())
            .Returns(snapshot);

        var mcpResult = await _client.CallToolAsync("get_page_markdown", new Dictionary<string, object?>
        {
            ["page_id"] = PageId,
        });

        var mcpText = mcpResult.Content.OfType<TextContentBlock>().First().Text;
        var mcpDoc = JsonDocument.Parse(mcpText);
        var mcpRevision = mcpDoc.RootElement.GetProperty("Revision").GetString();

        var cliOutput = await RunCliGetEditingAsync(_editor, PageId);
        var cliDoc = JsonDocument.Parse(cliOutput);
        var cliRevision = cliDoc.RootElement.GetProperty("revision").GetString();

        Assert.Equal(cliRevision, mcpRevision);
    }

    [Fact]
    public async Task PageNotFound_ThrowsMcpInvalidParams()
    {
        const string PageId = "nonexistent-page";

        _editor.FetchForEditAsync(PageId, Arg.Any<CancellationToken>())
            .Returns<Task<AnchoredPageSnapshot>>(_ =>
                throw new BuildinApiException(
                    new ApiError(404, "object_not_found", "Could not find page.", null)));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("get_page_markdown", new Dictionary<string, object?>
            {
                ["page_id"] = PageId,
            }));

        Assert.Equal(McpErrorCode.InvalidParams, ex.ErrorCode);
        Assert.Contains(PageId, ex.Message);
    }

    private static async Task<string> RunCliGetEditingAsync(IPageEditor editor, string pageId)
    {
        var renderer = Substitute.For<IPageMarkdownRenderer>();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(editor);
        services.AddSingleton(renderer);

        var testConsole = new Spectre.Console.Testing.TestConsole();
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
            config.AddCommand<GetCommand>("get");
        });

        var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        try
        {
            await app.RunAsync(["get", pageId, "--editing", "--print", "json"]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return sw.ToString().Trim();
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
