using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Buildin;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;
using Buildout.Core.Markdown;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Conversion.Mentions;
using Buildout.Core.Markdown.Internal;
using Buildout.IntegrationTests.Buildin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Buildout.IntegrationTests.Cli;

[Collection("BuildinWireMock")]
public sealed class GetCommandChildDatabaseTests
{
    private readonly BuildinWireMockFixture _fixture;

    private const string PageId = "cccccccc-cccc-cccc-cccc-cccccccccccc";
    private const string DatabaseId = "dddddddd-dddd-dddd-dddd-dddddddddddd";

    public GetCommandChildDatabaseTests(BuildinWireMockFixture fixture)
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
                [DatabaseViewStyle.Table] = new TableViewStyle()
            });
        services.AddSingleton<IDatabaseViewRenderer, DatabaseViewRenderer>();

        services.AddSingleton<IBlockToMarkdownConverter>(static sp =>
            new ChildDatabaseConverter(sp.GetRequiredService<IDatabaseViewRenderer>()));
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new ParagraphConverter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new Heading1Converter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new Heading2Converter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new Heading3Converter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new BulletedListItemConverter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new NumberedListItemConverter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new ToDoConverter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new CodeConverter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new QuoteConverter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new DividerConverter());

        services.AddSingleton<IMentionToMarkdownConverter>(static _ => new PageMentionConverter());
        services.AddSingleton<IMentionToMarkdownConverter>(static _ => new DatabaseMentionConverter());
        services.AddSingleton<IMentionToMarkdownConverter>(static _ => new UserMentionConverter());
        services.AddSingleton<IMentionToMarkdownConverter>(static _ => new DateMentionConverter());

        services.AddSingleton<BlockToMarkdownRegistry>();
        services.AddSingleton<MentionToMarkdownRegistry>();
        services.AddSingleton<IInlineRenderer, InlineRenderer>();
        services.AddSingleton<IPageMarkdownRenderer, PageMarkdownRenderer>();

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
            config.AddCommand<GetCommand>("get");
        });

        return (app, testConsole);
    }

    private void SetupFixtures()
    {
        BuildinStubs.RegisterGetPage(_fixture.Server, new
        {
            id = PageId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{PageId[..8]}",
            properties = new
            {
                title = new
                {
                    type = "title",
                    title = new[] { new { type = "text", plain_text = "My Page" } }
                }
            }
        });

        BuildinStubs.RegisterGetBlockChildren(_fixture.Server, new
        {
            @object = "list",
            results = new object[]
            {
                new
                {
                    id = DatabaseId,
                    type = "child_database",
                    created_time = "2025-01-01T00:00:00Z",
                    has_children = false,
                    data = new { title = "Embedded DB" }
                }
            },
            has_more = false
        });

        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            id = DatabaseId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            title = new[] { new { type = "text", plain_text = "Embedded DB" } },
            properties = new
            {
                Name = new { type = "title", title = new { } }
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
                        Name = new { type = "title", title = new[] { new { type = "text", plain_text = "Item One" } } }
                    }
                }
            },
            has_more = false,
            next_cursor = (string?)null
        });
    }

    [Fact]
    public async Task ChildDatabase_RenderedInline_ContainsDatabaseHeading()
    {
        var client = _fixture.CreateClient();
        SetupFixtures();
        var (app, console) = CreateApp(client);

        var exitCode = await app.RunAsync(["get", PageId]);

        Assert.Equal(0, exitCode);
        Assert.Contains("## Embedded DB", console.Output);
    }

    [Fact]
    public async Task ChildDatabase_RenderedInline_ContainsTableContent()
    {
        var client = _fixture.CreateClient();
        SetupFixtures();
        var (app, console) = CreateApp(client);

        var exitCode = await app.RunAsync(["get", PageId]);

        Assert.Equal(0, exitCode);
        Assert.Contains("## Embedded DB", console.Output);
        Assert.Contains("(no rows)", console.Output);
    }

    [Fact]
    public async Task ChildDatabase_PageTitle_StillRendered()
    {
        var client = _fixture.CreateClient();
        SetupFixtures();
        var (app, console) = CreateApp(client);

        var exitCode = await app.RunAsync(["get", PageId]);

        Assert.Equal(0, exitCode);
        Assert.Contains("# My Page", console.Output);
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
