using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Authentication;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;
using Buildout.IntegrationTests.Buildin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using Xunit;

namespace Buildout.IntegrationTests.Cli;

[Collection("BuildinWireMock")]
public sealed class DbViewCommandTests
{
    private readonly BuildinWireMockFixture _fixture;
    private const string DatabaseId = "dddddddd-dddd-dddd-dddd-dddddddddddd";

    public DbViewCommandTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private static (CommandApp app, TestConsole console) CreateApp(
        IBuildinClient client, bool styledStdout = false)
    {
        var services = new ServiceCollection();
        services.AddSingleton(client);
        services.AddSingleton<IPropertyValueFormatter, PropertyValueFormatter>();
        services.AddSingleton<CellBudget>(static _ => new CellBudget(24, "…"));
        services.AddSingleton<IReadOnlyDictionary<DatabaseViewStyle, IDatabaseViewStyle>>(
            static _ => new Dictionary<DatabaseViewStyle, IDatabaseViewStyle>
            {
                [DatabaseViewStyle.Table] = new TableViewStyle()
            });
        services.AddSingleton<IDatabaseViewRenderer, DatabaseViewRenderer>();
        services.AddSingleton<MarkdownTerminalRenderer>();

        var testConsole = new TestConsole();
        services.AddSingleton<Spectre.Console.IAnsiConsole>(testConsole);

        var caps = new TerminalCapabilities(
            isAnsi: styledStdout,
            isOutputRedirected: !styledStdout,
            noColorEnv: null);
        services.AddSingleton(caps);

        var pinnedTypes = new HashSet<Type> { typeof(Spectre.Console.IAnsiConsole) };
        var registrar = new TypeRegistrar(services, pinnedTypes);

        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.PropagateExceptions();
            config.AddBranch<DbSettings>("db", db =>
            {
                db.AddCommand<DbViewCommand>("view");
            });
        });

        return (app, testConsole);
    }

    private void SetupDatabaseWithThreeRows()
    {
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            id = DatabaseId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            title = new[]
            {
                new { type = "text", plain_text = "Test Database" }
            },
            properties = new
            {
                Name = new { type = "title", title = new { } },
                Status = new { type = "select", select = new { options = new[] { new { name = "Active" }, new { name = "Done" } } } },
                Priority = new { type = "number", number = new { format = "number" } }
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
                        Status = new { type = "select", select = new { name = "Active" } },
                        Priority = new { type = "number", number = 1 }
                    }
                },
                new
                {
                    properties = new
                    {
                        Name = new { type = "title", title = new[] { new { type = "text", plain_text = "Row 2" } } },
                        Status = new { type = "select", select = new { name = "Done" } },
                        Priority = new { type = "number", number = 2 }
                    }
                },
                new
                {
                    properties = new
                    {
                        Name = new { type = "title", title = new[] { new { type = "text", plain_text = "Row 3" } } },
                        Status = new { type = "select", select = new { name = "Active" } },
                        Priority = new { type = "number", number = 3 }
                    }
                }
            },
            has_more = false,
            next_cursor = (string?)null
        });
    }

    [Fact]
    public async Task Valid_table_view_returns_rendered_output()
    {
        var client = _fixture.CreateClient();
        SetupDatabaseWithThreeRows();
        var (app, console) = CreateApp(client, styledStdout: false);

        var exitCode = await app.RunAsync(["db", "view", DatabaseId]);
        Assert.Equal(0, exitCode);
        Assert.StartsWith("# Test Database — table view", console.Output);
    }

    [Fact]
    public async Task Database_not_found_returns_exit_3()
    {
        var client = _fixture.CreateClient();
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            status = 404,
            code = "not_found",
            message = "Database not found",
            @object = "error"
        }, statusCode: 404);
        var (app, _) = CreateApp(client, styledStdout: false);

        var exitCode = await app.RunAsync(["db", "view", DatabaseId]);
        Assert.Equal(3, exitCode);
    }

    [Fact]
    public async Task Auth_failure_returns_exit_4()
    {
        var client = _fixture.CreateClient();
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            status = 401,
            code = "unauthorized",
            message = "Invalid token",
            @object = "error"
        }, statusCode: 401);
        var (app, _) = CreateApp(client, styledStdout: false);

        var exitCode = await app.RunAsync(["db", "view", DatabaseId]);
        Assert.Equal(4, exitCode);
    }

    [Fact]
    public async Task Transport_error_returns_exit_5()
    {
        var deadClient = new BotBuildinClient(
            new HttpClient { BaseAddress = new Uri("http://localhost:1") },
            new BotTokenAuthenticationProvider("test-token"),
            Options.Create(new BuildinClientOptions()),
            LoggerFactory.Create(_ => { }).CreateLogger<BotBuildinClient>());
        var (app, _) = CreateApp(deadClient, styledStdout: false);

        var exitCode = await app.RunAsync(["db", "view", DatabaseId]);
        Assert.Equal(5, exitCode);
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
