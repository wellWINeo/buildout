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
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Buildout.IntegrationTests.Cross;

[Collection("BuildinWireMock")]
public sealed class ChildDatabasePlaceholderTests
{
    private readonly BuildinWireMockFixture _fixture;

    private const string PageId = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee";
    private const string DatabaseId = "ffffffff-ffff-ffff-ffff-ffffffffffff";

    public ChildDatabasePlaceholderTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private static (CommandApp app, TestConsole console) CreateApp(IBuildinClient client)
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

    private void SetupPage(string databaseTitle = "Failing DB")
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
                    title = new[] { new { type = "text", plain_text = "Test Page" } }
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
                    data = new { title = databaseTitle }
                }
            },
            has_more = false
        });
    }

    [Fact]
    public async Task DatabaseNotFound_PageRendersWithPlaceholder_ExitZero()
    {
        var client = _fixture.CreateClient();
        SetupPage("Failing DB");
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            status = 404,
            code = "not_found",
            message = "Database not found",
            @object = "error"
        }, statusCode: 404);

        var (app, console) = CreateApp(client);
        var exitCode = await app.RunAsync(["get", PageId]);

        Assert.Equal(0, exitCode);
        Assert.Contains("[child database: not found", console.Output);
        Assert.Contains("# Test Page", console.Output);
    }

    [Fact]
    public async Task DatabaseTransportError_PageRendersWithPlaceholder_ExitZero()
    {
        var client = _fixture.CreateClient();
        SetupPage("Failing DB");
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            status = 503,
            code = "service_unavailable",
            message = "Service unavailable",
            @object = "error"
        }, statusCode: 503);

        var (app, console) = CreateApp(client);
        var exitCode = await app.RunAsync(["get", PageId]);

        Assert.Equal(0, exitCode);
        Assert.Contains("[child database:", console.Output);
        Assert.Contains("# Test Page", console.Output);
    }

    [Fact]
    public async Task DatabaseAccessDenied_PageRendersWithPlaceholder_ExitZero()
    {
        var client = _fixture.CreateClient();
        SetupPage("Failing DB");
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            status = 403,
            code = "forbidden",
            message = "Access denied",
            @object = "error"
        }, statusCode: 403);

        var (app, console) = CreateApp(client);
        var exitCode = await app.RunAsync(["get", PageId]);

        Assert.Equal(0, exitCode);
        Assert.Contains("[child database: access denied", console.Output);
        Assert.Contains("# Test Page", console.Output);
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
