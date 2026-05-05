using System.Text;
using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Conversion.Mentions;
using Buildout.Core.Markdown.Internal;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Buildout.IntegrationTests.Cli;

public sealed class GetCommandTests
{
    private static (CommandApp app, TestConsole console, IBuildinClient client) CreateApp(
        bool styledStdout = false)
    {
        var client = Substitute.For<IBuildinClient>();

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

        var registrar = new TypeRegistrar(services);

        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.PropagateExceptions();
            config.AddCommand<GetCommand>("get");
        });

        return (app, testConsole, client);
    }

    private static void SetupPage(IBuildinClient client, string pageId, string title, params Block[] blocks)
    {
        client.GetPageAsync(pageId, Arg.Any<CancellationToken>())
            .Returns(new Page
            {
                Id = pageId,
                Title = [new RichText { Type = "text", Content = title }]
            });
        client.GetBlockChildrenAsync(pageId, Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = blocks.ToList(), HasMore = false });
    }

    [Fact]
    public async Task HappyPath_Exit0_AndStdoutContainsRenderedOutput()
    {
        var (app, _, client) = CreateApp(styledStdout: false);
        SetupPage(client, "page-1", "Test Page",
            new ParagraphBlock
            {
                Id = "b1",
                RichTextContent = [new RichText { Type = "text", Content = "Hello world" }]
            });

        var originalOut = Console.Out;
        var sb = new StringBuilder();
        await using var sw = new StringWriter(sb);
        Console.SetOut(sw);
        try
        {
            var exitCode = await app.RunAsync(["get", "page-1"]);
            Assert.Equal(0, exitCode);
            Assert.Contains("Test Page", sb.ToString());
            Assert.Contains("Hello world", sb.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task PlainMode_OutputMatchesCoreRenderer()
    {
        var (app, _, client) = CreateApp(styledStdout: false);
        SetupPage(client, "page-1", "Title",
            new ParagraphBlock
            {
                Id = "b1",
                RichTextContent = [new RichText { Type = "text", Content = "Body text" }]
            });

        var originalOut = Console.Out;
        var sb = new StringBuilder();
        await using var sw = new StringWriter(sb);
        Console.SetOut(sw);
        try
        {
            var exitCode = await app.RunAsync(["get", "page-1"]);
            Assert.Equal(0, exitCode);
            Assert.Contains("# Title", sb.ToString());
            Assert.Contains("Body text", sb.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RichMode_OutputContainsAnsiEscapes()
    {
        var (_, console, client) = CreateApp(styledStdout: true);
        SetupPage(client, "page-1", "Title",
            new ParagraphBlock
            {
                Id = "b1",
                RichTextContent = [new RichText { Type = "text", Content = "Body" }]
            });

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

        var markdown = await coreRenderer.RenderAsync("page-1");
        renderer.Render(markdown);

        var hasAnsiEscape = console.Output.Contains("\x1b[") || console.Output.Contains("\u001b[");
        Assert.True(hasAnsiEscape, "Rich mode output should contain ANSI escape codes");
    }

    [Fact]
    public async Task PlainMode_OutputContainsZeroAnsiEscapes()
    {
        var (app, _, client) = CreateApp(styledStdout: false);
        SetupPage(client, "page-1", "Title",
            new ParagraphBlock
            {
                Id = "b1",
                RichTextContent = [new RichText { Type = "text", Content = "Body" }]
            });

        var originalOut = Console.Out;
        var sb = new StringBuilder();
        await using var sw = new StringWriter(sb);
        Console.SetOut(sw);
        try
        {
            var exitCode = await app.RunAsync(["get", "page-1"]);
            Assert.Equal(0, exitCode);
            Assert.False(sb.ToString().Contains('\x1b'), "Plain mode should not contain ANSI escape codes");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task NotFound_ReturnsExit3()
    {
        var (app, _, client) = CreateApp(styledStdout: false);
        client.GetPageAsync("missing", Arg.Any<CancellationToken>())
            .Returns<Task<Page>>(_ => throw new BuildinApiException(
                new ApiError(404, "not_found", "Page not found", string.Empty)));

        var exitCode = await app.RunAsync(["get", "missing"]);
        Assert.Equal(3, exitCode);
    }

    [Fact]
    public async Task AuthFailure_ReturnsExit4()
    {
        var (app, _, client) = CreateApp(styledStdout: false);
        client.GetPageAsync("page-1", Arg.Any<CancellationToken>())
            .Returns<Task<Page>>(_ => throw new BuildinApiException(
                new ApiError(401, "unauthorized", "Invalid token", string.Empty)));

        var exitCode = await app.RunAsync(["get", "page-1"]);
        Assert.Equal(4, exitCode);
    }

    [Fact]
    public async Task Forbidden_ReturnsExit4()
    {
        var (app, _, client) = CreateApp(styledStdout: false);
        client.GetPageAsync("page-1", Arg.Any<CancellationToken>())
            .Returns<Task<Page>>(_ => throw new BuildinApiException(
                new ApiError(403, "forbidden", "Access denied", string.Empty)));

        var exitCode = await app.RunAsync(["get", "page-1"]);
        Assert.Equal(4, exitCode);
    }

    [Fact]
    public async Task TransportFailure_ReturnsExit5()
    {
        var (app, _, client) = CreateApp(styledStdout: false);
        client.GetPageAsync("page-1", Arg.Any<CancellationToken>())
            .Returns<Task<Page>>(_ => throw new BuildinApiException(
                new TransportError(new HttpRequestException("Connection refused"))));

        var exitCode = await app.RunAsync(["get", "page-1"]);
        Assert.Equal(5, exitCode);
    }

    [Fact]
    public async Task UnexpectedError_ReturnsExit6()
    {
        var (app, _, client) = CreateApp(styledStdout: false);
        client.GetPageAsync("page-1", Arg.Any<CancellationToken>())
            .Returns<Task<Page>>(_ => throw new BuildinApiException(
                new UnknownError(500, "Internal error")));

        var exitCode = await app.RunAsync(["get", "page-1"]);
        Assert.Equal(6, exitCode);
    }

    private sealed class TypeRegistrar : ITypeRegistrar
    {
        private readonly IServiceCollection _services;

        public TypeRegistrar(IServiceCollection services) => _services = services;

        public void Register(Type service, Type implementation) => _services.AddSingleton(service, implementation);
        public void RegisterInstance(Type service, object implementation) => _services.AddSingleton(service, implementation);
        public void RegisterLazy(Type service, Func<object> factory) => _services.AddSingleton(service, _ => factory());
        public ITypeResolver Build() => new TypeResolver(_services.BuildServiceProvider());
    }

    private sealed class TypeResolver : ITypeResolver
    {
        private readonly IServiceProvider _provider;
        public TypeResolver(IServiceProvider provider) => _provider = provider;
        public object? Resolve(Type? type) => type is null ? null : _provider.GetService(type);
    }
}
