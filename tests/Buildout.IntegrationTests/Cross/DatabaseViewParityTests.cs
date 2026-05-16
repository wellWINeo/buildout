using System.IO.Pipelines;
using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Buildin;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;
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
public sealed class DatabaseViewParityTests : IAsyncLifetime
{
    private readonly BuildinWireMockFixture _fixture;
    private const string DatabaseId = "dddddddd-dddd-dddd-dddd-dddddddddddd";

    private McpServer _server = null!;
    private McpClient _mcpClient = null!;
    private ServiceProvider _sp = null!;
    private Pipe _c2s = null!;
    private Pipe _s2c = null!;

    public DatabaseViewParityTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_fixture.CreateClient());
        services.AddSingleton<CellBudget>(static _ => new CellBudget(24, "…"));
        services.AddSingleton<IPropertyValueFormatter, PropertyValueFormatter>();
        services.AddSingleton<IReadOnlyDictionary<DatabaseViewStyle, IDatabaseViewStyle>>(
            static _ => new Dictionary<DatabaseViewStyle, IDatabaseViewStyle>
            {
                [DatabaseViewStyle.Table] = new TableViewStyle(),
                [DatabaseViewStyle.Board] = new BoardViewStyle(),
                [DatabaseViewStyle.Gallery] = new GalleryViewStyle(),
                [DatabaseViewStyle.List] = new ListViewStyle(),
                [DatabaseViewStyle.Calendar] = new CalendarViewStyle(),
                [DatabaseViewStyle.Timeline] = new TimelineViewStyle(),
            });
        services.AddSingleton<IDatabaseViewRenderer, DatabaseViewRenderer>();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddMcpServer().WithTools<DatabaseViewToolHandler>();

        _sp = services.BuildServiceProvider();

        var options = _sp.GetRequiredService<IOptions<McpServerOptions>>().Value;
        _c2s = new Pipe();
        _s2c = new Pipe();

        _server = McpServer.Create(
            new StreamServerTransport(_c2s.Reader.AsStream(), _s2c.Writer.AsStream()),
            options,
            _sp.GetRequiredService<ILoggerFactory>(),
            _sp);

        _ = _server.RunAsync();

        _mcpClient = await McpClient.CreateAsync(
            new StreamClientTransport(_c2s.Writer.AsStream(), _s2c.Reader.AsStream()),
            new McpClientOptions(),
            _sp.GetRequiredService<ILoggerFactory>());
    }

    public async ValueTask DisposeAsync()
    {
        await _mcpClient.DisposeAsync();
        await _server.DisposeAsync();
        _c2s.Writer.Complete();
        _c2s.Reader.Complete();
        _s2c.Writer.Complete();
        _s2c.Reader.Complete();
        await _sp.DisposeAsync();
    }

    private void SetupFixture()
    {
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            id = DatabaseId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            title = new[] { new { type = "text", plain_text = "Parity DB" } },
            properties = new
            {
                Name = new { type = "title", title = new { } },
                Status = new { type = "select", select = new { options = new[] { new { name = "Done" } } } }
            }
        });

        BuildinStubs.RegisterQueryDatabase(_fixture.Server, DatabaseId, new
        {
            results = new object[]
            {
                new
                {
                    properties = new
                    {
                        Name = new { type = "title", title = new[] { new { type = "text", plain_text = "Row 1" } } },
                        Status = new { type = "select", select = new { name = "Done" } }
                    }
                }
            },
            has_more = false,
            next_cursor = (string?)null
        });
    }

    private (CommandApp app, TestConsole console) CreateCliApp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_fixture.CreateClient());
        services.AddSingleton<CellBudget>(static _ => new CellBudget(24, "…"));
        services.AddSingleton<IPropertyValueFormatter, PropertyValueFormatter>();
        services.AddSingleton<IReadOnlyDictionary<DatabaseViewStyle, IDatabaseViewStyle>>(
            static _ => new Dictionary<DatabaseViewStyle, IDatabaseViewStyle>
            {
                [DatabaseViewStyle.Table] = new TableViewStyle(),
                [DatabaseViewStyle.Gallery] = new GalleryViewStyle(),
                [DatabaseViewStyle.List] = new ListViewStyle(),
            });
        services.AddSingleton<IDatabaseViewRenderer, DatabaseViewRenderer>();
        services.AddSingleton<MarkdownTerminalRenderer>();

        var console = new TestConsole();
        services.AddSingleton<Spectre.Console.IAnsiConsole>(console);

        var caps = new TerminalCapabilities(isAnsi: false, isOutputRedirected: true, noColorEnv: null);
        services.AddSingleton(caps);

        var pinnedTypes = new HashSet<Type> { typeof(Spectre.Console.IAnsiConsole) };
        var registrar = new TypeRegistrar(services, pinnedTypes);

        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.PropagateExceptions();
            config.AddBranch<DbSettings>("db", db => db.AddCommand<DbViewCommand>("view"));
        });

        return (app, console);
    }

    [Theory]
    [InlineData("table", null, null)]
    [InlineData("list", null, null)]
    [InlineData("gallery", null, null)]
    public async Task CliAndMcp_ProduceByteIdenticalOutput(string style, string? groupBy, string? dateProperty)
    {
        SetupFixture();

        var (cliApp, cliConsole) = CreateCliApp();
        var cliArgs = new List<string> { "db", "view", DatabaseId, "--style", style };
        if (groupBy is not null) { cliArgs.Add("--group-by"); cliArgs.Add(groupBy); }
        if (dateProperty is not null) { cliArgs.Add("--date-property"); cliArgs.Add(dateProperty); }

        await cliApp.RunAsync(cliArgs);
        var cliOutput = cliConsole.Output;

        var mcpArgs = new Dictionary<string, object?> { ["database_id"] = DatabaseId, ["style"] = style };
        if (groupBy is not null) mcpArgs["group_by"] = groupBy;
        if (dateProperty is not null) mcpArgs["date_property"] = dateProperty;

        var mcpResult = await _mcpClient.CallToolAsync("database_view", mcpArgs);
        var mcpOutput = mcpResult.Content.OfType<TextContentBlock>().First().Text;

        Assert.Equal(mcpOutput, cliOutput);
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
