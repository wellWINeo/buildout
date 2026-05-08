using System.IO.Pipelines;
using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Search;
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

public sealed class SearchToolTests : IAsyncLifetime
{
    private readonly ISearchService _service = Substitute.For<ISearchService>();
    private ServiceProvider _sp = null!;
    private McpServer _server = null!;
    private McpClient _client = null!;
    private Pipe _c2s = null!;
    private Pipe _s2c = null!;

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISearchService>(_service);
        services.AddSingleton<ISearchResultFormatter, SearchResultFormatter>();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));

        services.AddMcpServer().WithTools<SearchToolHandler>();

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
    public async Task ServerAdvertisesSearchTool()
    {
        var tools = await _client.ListToolsAsync();

        Assert.Single(tools);
        Assert.Equal("search", tools[0].Name);
        Assert.NotNull(tools[0].Description);
    }

    [Fact]
    public async Task Search_ReturnsFormattedBody()
    {
        var matches = new List<SearchMatch>
        {
            new() { PageId = "abc-123", ObjectType = SearchObjectType.Page, DisplayTitle = "Hello" },
            new() { PageId = "def-456", ObjectType = SearchObjectType.Database, DisplayTitle = "World" },
        };

        _service.SearchAsync("test", null, Arg.Any<CancellationToken>())
            .Returns(matches);

        var formatter = new SearchResultFormatter();
        var expected = formatter.Format(matches);

        var result = await _client.CallToolAsync("search", new Dictionary<string, object?> { ["query"] = "test" });

        var text = result.Content.OfType<TextContentBlock>().First().Text;
        Assert.Equal(expected, text);
    }

    [Fact]
    public async Task Search_NoMatches_ReturnsEmptyBody()
    {
        _service.SearchAsync("nothing", null, Arg.Any<CancellationToken>())
            .Returns(new List<SearchMatch>());

        var result = await _client.CallToolAsync("search", new Dictionary<string, object?> { ["query"] = "nothing" });

        var text = result.Content.OfType<TextContentBlock>().First().Text;
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public async Task EmptyQuery_ThrowsMcpInvalidParams()
    {
        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("search", new Dictionary<string, object?> { ["query"] = "" }));

        Assert.Equal(McpErrorCode.InvalidParams, ex.ErrorCode);

        await _service.DidNotReceiveWithAnyArgs().SearchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthFailure_ThrowsInternalError()
    {
        _service.SearchAsync("auth", null, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SearchMatch>>>(_ => throw new BuildinApiException(
                new ApiError(401, "unauthorized", "Unauthorized", null)));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("search", new Dictionary<string, object?> { ["query"] = "auth" }));

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
    }

    [Fact]
    public async Task TransportFailure_ThrowsInternalError()
    {
        _service.SearchAsync("transport", null, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SearchMatch>>>(_ => throw new BuildinApiException(
                new TransportError(new HttpRequestException("Connection refused"))));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("search", new Dictionary<string, object?> { ["query"] = "transport" }));

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
    }

    [Fact]
    public async Task ScopedSearch_ReturnsOnlyDescendants()
    {
        var scopedMatches = new List<SearchMatch>
        {
            new() { PageId = "a-id", ObjectType = SearchObjectType.Page, DisplayTitle = "Page A" },
            new() { PageId = "b-id", ObjectType = SearchObjectType.Page, DisplayTitle = "Page B" },
            new() { PageId = "c-id", ObjectType = SearchObjectType.Page, DisplayTitle = "Page C" },
        };

        _service.SearchAsync("q", "a-id", Arg.Any<CancellationToken>())
            .Returns(scopedMatches);

        var expected = new SearchResultFormatter().Format(scopedMatches);

        var result = await _client.CallToolAsync("search", new Dictionary<string, object?> { ["query"] = "q", ["page_id"] = "a-id" });

        var text = result.Content.OfType<TextContentBlock>().First().Text;
        Assert.Equal(expected, text);
    }

    [Fact]
    public async Task ScopedSearch_UnrelatedPage_ReturnsEmptyBody()
    {
        _service.SearchAsync("q", "unrelated-id", Arg.Any<CancellationToken>())
            .Returns(new List<SearchMatch>());

        var result = await _client.CallToolAsync("search", new Dictionary<string, object?> { ["query"] = "q", ["page_id"] = "unrelated-id" });

        var text = result.Content.OfType<TextContentBlock>().First().Text;
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public async Task ScopedSearch_MissingPage_ThrowsResourceNotFound()
    {
        _service.SearchAsync("q", "missing-id", Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SearchMatch>>>(_ => throw new BuildinApiException(
                new ApiError(404, "object_not_found", "Could not find page", null)));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("search", new Dictionary<string, object?> { ["query"] = "q", ["page_id"] = "missing-id" }));

        Assert.Equal(McpErrorCode.ResourceNotFound, ex.ErrorCode);
    }

    [Fact]
    public async Task ToolResultBody_EqualsCliPlainBody()
    {
        var matches = new List<SearchMatch>
        {
            new() { PageId = "x1", ObjectType = SearchObjectType.Page, DisplayTitle = "Alpha" },
            new() { PageId = "x2", ObjectType = SearchObjectType.Database, DisplayTitle = "Beta" },
        };

        _service.SearchAsync("eq-test", null, Arg.Any<CancellationToken>())
            .Returns(matches);

        var mcpResult = await _client.CallToolAsync("search", new Dictionary<string, object?> { ["query"] = "eq-test" });
        var mcpBody = mcpResult.Content.OfType<TextContentBlock>().First().Text;

        var cliBody = await RunCliAsync(_service, ["search", "eq-test"]);

        Assert.Equal(cliBody, mcpBody);
    }

    [Fact]
    public async Task ScopedToolResultBody_EqualsCliPlainBody()
    {
        var matches = new List<SearchMatch>
        {
            new() { PageId = "s1", ObjectType = SearchObjectType.Page, DisplayTitle = "Scoped" },
        };

        _service.SearchAsync("sc-test", "scope-id", Arg.Any<CancellationToken>())
            .Returns(matches);

        var mcpResult = await _client.CallToolAsync("search", new Dictionary<string, object?> { ["query"] = "sc-test", ["page_id"] = "scope-id" });
        var mcpBody = mcpResult.Content.OfType<TextContentBlock>().First().Text;

        var cliBody = await RunCliAsync(_service, ["search", "sc-test", "--page", "scope-id"]);

        Assert.Equal(cliBody, mcpBody);
    }

    private static async Task<string> RunCliAsync(ISearchService service, string[] args)
    {
        var formatter = new SearchResultFormatter();

        var services = new ServiceCollection();
        services.AddSingleton(service);
        services.AddSingleton<ISearchResultFormatter>(formatter);

        var testConsole = new Spectre.Console.Testing.TestConsole();
        services.AddSingleton<Spectre.Console.IAnsiConsole>(testConsole);

        var caps = new TerminalCapabilities(isAnsi: false, isOutputRedirected: true, noColorEnv: null);
        services.AddSingleton(caps);
        services.AddSingleton<SearchResultStyledRenderer>();

        // Pin IAnsiConsole so the TypeRegistrar ignores Spectre's attempt to override it
        var pinnedTypes = new HashSet<Type> { typeof(Spectre.Console.IAnsiConsole) };
        var registrar = new TypeRegistrar(services, pinnedTypes);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.PropagateExceptions();
            config.AddCommand<SearchCommand>("search");
        });

        await app.RunAsync(args);
        return testConsole.Output;
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
