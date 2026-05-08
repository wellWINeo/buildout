using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Authentication;
using Buildout.Core.Markdown;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Conversion.Mentions;
using Buildout.Core.Markdown.Internal;
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
public sealed class GetCommandTests
{
    private readonly BuildinWireMockFixture _fixture;

    public GetCommandTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private static (CommandApp app, TestConsole console) CreateApp(
        IBuildinClient client, bool styledStdout = false)
    {
        var services = new ServiceCollection();
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
        services.AddSingleton(client);

        var testConsole = new TestConsole();
        services.AddSingleton<Spectre.Console.IAnsiConsole>(testConsole);

        var caps = new TerminalCapabilities(
            isAnsi: styledStdout,
            isOutputRedirected: !styledStdout,
            noColorEnv: null);
        services.AddSingleton(caps);
        services.AddSingleton<MarkdownTerminalRenderer>();

        // Pin IAnsiConsole so the TypeRegistrar ignores Spectre's attempt to override it
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

    private void SetupPage(string pageId, string title, string blockType, string blockText)
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
                    type = blockType,
                    created_time = "2025-01-01T00:00:00Z",
                    has_children = false,
                    data = new { rich_text = new[] { new { type = "text", plain_text = blockText } } }
                }
            },
            has_more = false
        });
    }

    [Fact]
    public async Task HappyPath_Exit0_AndStdoutContainsRenderedOutput()
    {
        var client = _fixture.CreateClient();
        SetupPage("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Test Page", "paragraph", "Hello world");
        var (app, console) = CreateApp(client, styledStdout: false);

        var exitCode = await app.RunAsync(["get", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"]);
        Assert.Equal(0, exitCode);
        Assert.Contains("Test Page", console.Output);
        Assert.Contains("Hello world", console.Output);
    }

    [Fact]
    public async Task PlainMode_OutputMatchesCoreRenderer()
    {
        var client = _fixture.CreateClient();
        SetupPage("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Title", "paragraph", "Body text");
        var (app, console) = CreateApp(client, styledStdout: false);

        var exitCode = await app.RunAsync(["get", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"]);
        Assert.Equal(0, exitCode);
        Assert.Contains("# Title", console.Output);
        Assert.Contains("Body text", console.Output);
    }

    [Fact]
    public async Task RichMode_OutputContainsAnsiEscapes()
    {
        var client = _fixture.CreateClient();
        SetupPage("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Title", "paragraph", "Body");

        var console = new TestConsole();
        console.EmitAnsiSequences();

        var renderer = new MarkdownTerminalRenderer(console);
        var coreRenderer = new PageMarkdownRenderer(
            client,
            new BlockToMarkdownRegistry(new List<IBlockToMarkdownConverter>
            {
                new ParagraphConverter(), new Heading1Converter(), new Heading2Converter(),
                new Heading3Converter(), new BulletedListItemConverter(), new NumberedListItemConverter(),
                new ToDoConverter(), new CodeConverter(), new QuoteConverter(), new DividerConverter()
            }),
            new InlineRenderer(new MentionToMarkdownRegistry(new List<IMentionToMarkdownConverter>
            {
                new PageMentionConverter(), new DatabaseMentionConverter(),
                new UserMentionConverter(), new DateMentionConverter()
            })));

        var markdown = await coreRenderer.RenderAsync("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        renderer.Render(markdown);

        var hasAnsiEscape = console.Output.Contains("\x1b[") || console.Output.Contains('[');
        Assert.True(hasAnsiEscape, "Rich mode output should contain ANSI escape codes");
    }

    [Fact]
    public async Task PlainMode_OutputContainsZeroAnsiEscapes()
    {
        var client = _fixture.CreateClient();
        SetupPage("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Title", "paragraph", "Body");
        var (app, console) = CreateApp(client, styledStdout: false);

        var exitCode = await app.RunAsync(["get", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"]);
        Assert.Equal(0, exitCode);
        Assert.False(console.Output.Contains('\x1b'), "Plain mode should not contain ANSI escape codes");
    }

    [Fact]
    public async Task NotFound_ReturnsExit3()
    {
        var client = _fixture.CreateClient();
        BuildinStubs.RegisterGetPage(_fixture.Server, new
        {
            status = 404,
            code = "not_found",
            message = "Page not found",
            @object = "error"
        }, statusCode: 404);
        var (app, _) = CreateApp(client, styledStdout: false);

        var exitCode = await app.RunAsync(["get", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"]);
        Assert.Equal(3, exitCode);
    }

    [Fact]
    public async Task AuthFailure_ReturnsExit4()
    {
        var client = _fixture.CreateClient();
        BuildinStubs.RegisterGetPage(_fixture.Server, new
        {
            status = 401,
            code = "unauthorized",
            message = "Invalid token",
            @object = "error"
        }, statusCode: 401);
        var (app, _) = CreateApp(client, styledStdout: false);

        var exitCode = await app.RunAsync(["get", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"]);
        Assert.Equal(4, exitCode);
    }

    [Fact]
    public async Task Forbidden_ReturnsExit4()
    {
        var client = _fixture.CreateClient();
        BuildinStubs.RegisterGetPage(_fixture.Server, new
        {
            status = 403,
            code = "forbidden",
            message = "Access denied",
            @object = "error"
        }, statusCode: 403);
        var (app, _) = CreateApp(client, styledStdout: false);

        var exitCode = await app.RunAsync(["get", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"]);
        Assert.Equal(4, exitCode);
    }

    [Fact]
    public async Task TransportFailure_ReturnsExit5()
    {
        var deadClient = new BotBuildinClient(
            new HttpClient { BaseAddress = new Uri("http://localhost:1") },
            new BotTokenAuthenticationProvider("test-token"),
            Options.Create(new BuildinClientOptions()),
            LoggerFactory.Create(_ => { }).CreateLogger<BotBuildinClient>());
        var (app, _) = CreateApp(deadClient, styledStdout: false);

        var exitCode = await app.RunAsync(["get", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"]);
        Assert.Equal(5, exitCode);
    }

    [Fact]
    public async Task UnexpectedError_ReturnsExit6()
    {
        var client = _fixture.CreateClient();
        BuildinStubs.RegisterGetPage(_fixture.Server, new
        {
            status = 500,
            code = "internal_error",
            message = "Internal error",
            @object = "error"
        }, statusCode: 500);
        var (app, _) = CreateApp(client, styledStdout: false);

        var exitCode = await app.RunAsync(["get", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"]);
        Assert.Equal(6, exitCode);
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
