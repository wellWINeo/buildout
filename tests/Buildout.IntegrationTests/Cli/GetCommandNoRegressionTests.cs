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
public sealed class GetCommandNoRegressionTests
{
    private readonly BuildinWireMockFixture _fixture;

    public GetCommandNoRegressionTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private static (CommandApp app, TestConsole console) CreateAppWithChildDatabaseConverter(
        IBuildinClient client)
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

    private void SetupParagraphPage(string pageId, string title, string paragraphText)
    {
        BuildinStubs.RegisterGetPage(_fixture.Server, new
        {
            id = pageId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{pageId[..8]}",
            properties = new
            {
                title = new
                {
                    type = "title",
                    title = new[] { new { type = "text", plain_text = title } }
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
                    id = "b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1",
                    type = "paragraph",
                    created_time = "2025-01-01T00:00:00Z",
                    has_children = false,
                    data = new { rich_text = new[] { new { type = "text", plain_text = paragraphText } } }
                }
            },
            has_more = false
        });
    }

    [Fact]
    public async Task ParagraphPage_RendersCorrectly_WithChildDatabaseConverterRegistered()
    {
        var client = _fixture.CreateClient();
        SetupParagraphPage("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "My Title", "Hello world");

        var (app, console) = CreateAppWithChildDatabaseConverter(client);
        var exitCode = await app.RunAsync(["get", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("# My Title", console.Output);
        Assert.Contains("Hello world", console.Output);
    }

    [Fact]
    public async Task HeadingPage_RendersCorrectly_WithChildDatabaseConverterRegistered()
    {
        var client = _fixture.CreateClient();

        BuildinStubs.RegisterGetPage(_fixture.Server, new
        {
            id = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = "https://api.buildin.ai/pages/aaaaaaaa",
            properties = new
            {
                title = new
                {
                    type = "title",
                    title = new[] { new { type = "text", plain_text = "Doc Title" } }
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
                    id = "b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2",
                    type = "heading_1",
                    created_time = "2025-01-01T00:00:00Z",
                    has_children = false,
                    data = new { rich_text = new[] { new { type = "text", plain_text = "Section Header" } } }
                }
            },
            has_more = false
        });

        var (app, console) = CreateAppWithChildDatabaseConverter(client);
        var exitCode = await app.RunAsync(["get", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("# Doc Title", console.Output);
        Assert.Contains("Section Header", console.Output);
    }

    [Fact]
    public async Task ChildDatabaseConverterRegistered_DoesNotAffectNonDatabasePages_ExitZero()
    {
        var client = _fixture.CreateClient();
        SetupParagraphPage("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Plain Page", "Plain content");

        var (app, console) = CreateAppWithChildDatabaseConverter(client);
        var exitCode = await app.RunAsync(["get", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"]);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("[child database", console.Output);
        Assert.Contains("Plain content", console.Output);
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
