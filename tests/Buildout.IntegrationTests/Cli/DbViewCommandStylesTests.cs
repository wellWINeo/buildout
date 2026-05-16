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
using Xunit;

namespace Buildout.IntegrationTests.Cli;

[Collection("BuildinWireMock")]
public sealed class DbViewCommandStylesTests
{
    private readonly BuildinWireMockFixture _fixture;
    private const string DatabaseId = "dddddddd-dddd-dddd-dddd-dddddddddddd";

    public DbViewCommandStylesTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private static (CommandApp app, TestConsole console) CreateApp(IBuildinClient client)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(client);
        services.AddSingleton<IPropertyValueFormatter, PropertyValueFormatter>();
        services.AddSingleton<CellBudget>(static _ => new CellBudget(24, "…"));
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
        services.AddSingleton<MarkdownTerminalRenderer>();

        var testConsole = new TestConsole();
        services.AddSingleton<Spectre.Console.IAnsiConsole>(testConsole);

        var caps = new TerminalCapabilities(isAnsi: false, isOutputRedirected: true, noColorEnv: null);
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

    private void SetupBoardFixture()
    {
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            id = DatabaseId,
            title = new[] { new { type = "text", plain_text = "Board DB" } },
            properties = new
            {
                Name = new { type = "title", title = new { } },
                Status = new { type = "select", select = new { options = new[] { new { name = "Active" }, new { name = "Done" } } } }
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
                        Status = new { type = "select", select = new { name = "Active" } }
                    }
                },
                new
                {
                    properties = new
                    {
                        Name = new { type = "title", title = new[] { new { type = "text", plain_text = "Row 2" } } },
                        Status = new { type = "select", select = new { name = "Done" } }
                    }
                }
            },
            has_more = false,
            next_cursor = (string?)null
        });
    }

    private void SetupGalleryFixture()
    {
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            id = DatabaseId,
            title = new[] { new { type = "text", plain_text = "Gallery DB" } },
            properties = new
            {
                Name = new { type = "title", title = new { } },
                Status = new { type = "select", select = new { options = new[] { new { name = "Active" } } } }
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
                        Name = new { type = "title", title = new[] { new { type = "text", plain_text = "Gallery Row" } } },
                        Status = new { type = "select", select = new { name = "Active" } }
                    }
                }
            },
            has_more = false,
            next_cursor = (string?)null
        });
    }

    private void SetupListFixture()
    {
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            id = DatabaseId,
            title = new[] { new { type = "text", plain_text = "List DB" } },
            properties = new
            {
                Name = new { type = "title", title = new { } },
                Notes = new { type = "rich_text", rich_text = new { } }
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
                        Name = new { type = "title", title = new[] { new { type = "text", plain_text = "List Row" } } },
                        Notes = new { type = "rich_text", rich_text = new[] { new { type = "text", plain_text = "some note" } } }
                    }
                }
            },
            has_more = false,
            next_cursor = (string?)null
        });
    }

    private void SetupCalendarFixture()
    {
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            id = DatabaseId,
            title = new[] { new { type = "text", plain_text = "Calendar DB" } },
            properties = new
            {
                Name = new { type = "title", title = new { } },
                Due = new { type = "date", date = new { } }
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
                        Name = new { type = "title", title = new[] { new { type = "text", plain_text = "Cal Row" } } },
                        Due = new { type = "date", date = new { start = "2025-05-15", end = (string?)null } }
                    }
                }
            },
            has_more = false,
            next_cursor = (string?)null
        });
    }

    private void SetupTimelineFixture()
    {
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            id = DatabaseId,
            title = new[] { new { type = "text", plain_text = "Timeline DB" } },
            properties = new
            {
                Name = new { type = "title", title = new { } },
                Phase = new { type = "date", date = new { } }
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
                        Name = new { type = "title", title = new[] { new { type = "text", plain_text = "Phase Row" } } },
                        Phase = new { type = "date", date = new { start = "2025-06-01", end = "2025-06-30" } }
                    }
                }
            },
            has_more = false,
            next_cursor = (string?)null
        });
    }

    [Fact]
    public async Task Board_style_with_group_by_returns_exit_0()
    {
        var client = _fixture.CreateClient();
        SetupBoardFixture();
        var (app, console) = CreateApp(client);

        var exitCode = await app.RunAsync(["db", "view", DatabaseId, "--style", "board", "--group-by", "Status"]);

        Assert.Equal(0, exitCode);
        Assert.StartsWith("# Board DB — board view", console.Output);
    }

    [Fact]
    public async Task Gallery_style_returns_exit_0()
    {
        var client = _fixture.CreateClient();
        SetupGalleryFixture();
        var (app, console) = CreateApp(client);

        var exitCode = await app.RunAsync(["db", "view", DatabaseId, "--style", "gallery"]);

        Assert.Equal(0, exitCode);
        Assert.StartsWith("# Gallery DB — gallery view", console.Output);
    }

    [Fact]
    public async Task List_style_returns_exit_0()
    {
        var client = _fixture.CreateClient();
        SetupListFixture();
        var (app, console) = CreateApp(client);

        var exitCode = await app.RunAsync(["db", "view", DatabaseId, "--style", "list"]);

        Assert.Equal(0, exitCode);
        Assert.StartsWith("# List DB — list view", console.Output);
    }

    [Fact]
    public async Task Calendar_style_with_date_property_returns_exit_0()
    {
        var client = _fixture.CreateClient();
        SetupCalendarFixture();
        var (app, console) = CreateApp(client);

        var exitCode = await app.RunAsync(["db", "view", DatabaseId, "--style", "calendar", "--date-property", "Due"]);

        Assert.Equal(0, exitCode);
        Assert.StartsWith("# Calendar DB — calendar view", console.Output);
    }

    [Fact]
    public async Task Timeline_style_with_date_property_returns_exit_0()
    {
        var client = _fixture.CreateClient();
        SetupTimelineFixture();
        var (app, console) = CreateApp(client);

        var exitCode = await app.RunAsync(["db", "view", DatabaseId, "--style", "timeline", "--date-property", "Phase"]);

        Assert.Equal(0, exitCode);
        Assert.StartsWith("# Timeline DB — timeline view", console.Output);
    }

    [Fact]
    public async Task Board_style_without_group_by_returns_exit_2()
    {
        var client = _fixture.CreateClient();
        var (app, _) = CreateApp(client);

        var exitCode = await app.RunAsync(["db", "view", DatabaseId, "--style", "board"]);

        Assert.Equal(2, exitCode);
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
