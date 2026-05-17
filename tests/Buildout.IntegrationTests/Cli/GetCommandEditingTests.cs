using System.Text.Json;
using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Buildin;
using Buildout.Core.Markdown;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Conversion.Mentions;
using Buildout.Core.Markdown.Editing;
using Buildout.Core.Markdown.Internal;
using Buildout.IntegrationTests.Buildin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Buildout.IntegrationTests.Cli;

[Collection("BuildinWireMock")]
public sealed class GetCommandEditingTests
{
    private readonly BuildinWireMockFixture _fixture;
    private const string PageId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    public GetCommandEditingTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private static (CommandApp app, TestConsole console) CreateApp(IBuildinClient client)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton(client);
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
        services.AddSingleton<IMarkdownToBlocksParser, MarkdownToBlocksParser>();
        services.AddSingleton<IOptions<PageEditorOptions>>(static _ => Options.Create(new PageEditorOptions()));
        services.AddSingleton<IPageEditor, PageEditor>();

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

    private void SetupPageWithParagraph()
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
                    id = "b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1",
                    type = "paragraph",
                    created_time = "2025-01-01T00:00:00Z",
                    has_children = false,
                    data = new { rich_text = new[] { new { type = "text", plain_text = "Hello world" } } }
                }
            },
            has_more = false
        });
    }

    [Fact]
    public async Task Editing_PrintJson_ReturnsStructuredTriple()
    {
        var client = _fixture.CreateClient();
        SetupPageWithParagraph();
        var (app, _) = CreateApp(client);

        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            var exitCode = await app.RunAsync(["get", PageId, "--editing", "--print", "json"]);
            Assert.Equal(0, exitCode);

            var json = JsonDocument.Parse(outWriter.ToString());

            Assert.True(json.RootElement.TryGetProperty("markdown", out _));
            Assert.True(json.RootElement.TryGetProperty("revision", out var revision));
            Assert.True(json.RootElement.TryGetProperty("unknown_block_ids", out var unknownIds));
            Assert.Equal(JsonValueKind.Array, unknownIds.ValueKind);

            var revisionStr = revision.GetString();
            Assert.NotNull(revisionStr);
            Assert.Equal(8, revisionStr.Length);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task Editing_DefaultMode_WritesAnchoredMarkdownToStdout()
    {
        var client = _fixture.CreateClient();
        SetupPageWithParagraph();
        var (app, _) = CreateApp(client);

        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            var exitCode = await app.RunAsync(["get", PageId, "--editing"]);
            Assert.Equal(0, exitCode);
            Assert.Contains("<!-- buildin:root -->", outWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task Editing_DefaultMode_WritesRevisionToStderr()
    {
        var client = _fixture.CreateClient();
        SetupPageWithParagraph();
        var (app, _) = CreateApp(client);

        var originalErr = Console.Error;
        using var errWriter = new StringWriter();
        Console.SetError(errWriter);

        try
        {
            var exitCode = await app.RunAsync(["get", PageId, "--editing"]);
            Assert.Equal(0, exitCode);

            Assert.Matches(@"revision:\s+[0-9a-f]{8}", errWriter.ToString());
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public async Task WithoutEditing_ProducesUnanchoredMarkdown()
    {
        var client = _fixture.CreateClient();
        SetupPageWithParagraph();
        var (app, console) = CreateApp(client);

        var exitCode = await app.RunAsync(["get", PageId]);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("<!-- buildin:", console.Output);
        Assert.Contains("Test Page", console.Output);
        Assert.Contains("Hello world", console.Output);
    }

    [Fact]
    public async Task PrintJson_WithoutEditing_Exits2()
    {
        var client = _fixture.CreateClient();
        SetupPageWithParagraph();
        var (app, console) = CreateApp(client);

        var exitCode = await app.RunAsync(["get", PageId, "--print", "json"]);

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
