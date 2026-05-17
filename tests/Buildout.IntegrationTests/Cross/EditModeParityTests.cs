using System.IO.Pipelines;
using System.Text.Json;
using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Markdown;
using Buildout.Core.Markdown.Editing;
using Buildout.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Buildout.IntegrationTests.Cross;

public sealed class EditModeParityTests
{
    private const string PageId = "page-parity-001";

    // SC-007 partial: CLI get --editing --print json and MCP get_page_markdown produce equivalent triples.
    [Fact]
    public async Task FetchForEdit_CliAndMcp_ReturnEquivalentTriple()
    {
        var snapshot = new AnchoredPageSnapshot
        {
            Markdown = "<!-- buildin:root -->\n\n<!-- buildin:block:p1 -->\nHello world\n",
            Revision = "abc12345",
            UnknownBlockIds = [],
        };

        var editor = Substitute.For<IPageEditor>();
        editor.FetchForEditAsync(PageId, Arg.Any<CancellationToken>()).Returns(snapshot);

        var cliJson = await RunCliGetEditingJsonAsync(editor, PageId);
        var mcpJson = await RunMcpGetPageMarkdownAsync(editor, PageId);

        var cliDoc = JsonDocument.Parse(cliJson);
        var mcpDoc = JsonDocument.Parse(mcpJson);

        // CLI uses camelCase, MCP uses PascalCase
        var cliMarkdown = cliDoc.RootElement.GetProperty("markdown").GetString();
        var mcpMarkdown = mcpDoc.RootElement.GetProperty("Markdown").GetString();
        Assert.Equal(cliMarkdown, mcpMarkdown);

        var cliRevision = cliDoc.RootElement.GetProperty("revision").GetString();
        var mcpRevision = mcpDoc.RootElement.GetProperty("Revision").GetString();
        Assert.Equal(cliRevision, mcpRevision);
        Assert.Equal("abc12345", cliRevision);

        var cliIds = cliDoc.RootElement.GetProperty("unknown_block_ids").EnumerateArray().ToList();
        var mcpIds = mcpDoc.RootElement.GetProperty("UnknownBlockIds").EnumerateArray().ToList();
        Assert.Empty(cliIds);
        Assert.Empty(mcpIds);
    }

    // Both CLI update --print json and MCP update_page produce equivalent reconciliation summary shapes.
    [Fact]
    public async Task Update_CliAndMcp_ReturnEquivalentSummaryShape()
    {
        var summary = new ReconciliationSummary
        {
            PreservedBlocks = 3,
            UpdatedBlocks = 1,
            NewBlocks = 0,
            DeletedBlocks = 0,
            AmbiguousMatches = 0,
            NewRevision = "def67890",
            PostEditMarkdown = null,
        };

        var editor = Substitute.For<IPageEditor>();
        editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>()).Returns(summary);

        var cliJson = await RunCliUpdateJsonAsync(editor, PageId, "rev-old", """[{"op":"search_replace","old_str":"Hello","new_str":"World"}]""");
        var mcpJson = await RunMcpUpdatePageAsync(editor, PageId, "rev-old", """[{"op":"search_replace","old_str":"Hello","new_str":"World"}]""");

        var cliDoc = JsonDocument.Parse(cliJson);
        var mcpDoc = JsonDocument.Parse(mcpJson);

        // CLI uses camelCase, MCP uses PascalCase
        Assert.Equal(1, cliDoc.RootElement.GetProperty("updatedBlocks").GetInt32());
        Assert.Equal(1, mcpDoc.RootElement.GetProperty("UpdatedBlocks").GetInt32());

        Assert.Equal("def67890", cliDoc.RootElement.GetProperty("newRevision").GetString());
        Assert.Equal("def67890", mcpDoc.RootElement.GetProperty("NewRevision").GetString());

        Assert.Equal(3, cliDoc.RootElement.GetProperty("preservedBlocks").GetInt32());
        Assert.Equal(3, mcpDoc.RootElement.GetProperty("PreservedBlocks").GetInt32());

        Assert.Equal(0, cliDoc.RootElement.GetProperty("newBlocks").GetInt32());
        Assert.Equal(0, mcpDoc.RootElement.GetProperty("NewBlocks").GetInt32());

        Assert.Equal(0, cliDoc.RootElement.GetProperty("deletedBlocks").GetInt32());
        Assert.Equal(0, mcpDoc.RootElement.GetProperty("DeletedBlocks").GetInt32());
    }

    // SC-003: CLI get <id> (no --editing) outputs exactly what IPageMarkdownRenderer.RenderAsync returns.
    [Fact]
    public async Task Get_WithoutEditing_MatchesPageMarkdownRenderer()
    {
        const string ExpectedMarkdown = "# Hello\n\nWorld\n";

        var editor = Substitute.For<IPageEditor>();
        var renderer = Substitute.For<IPageMarkdownRenderer>();
        renderer.RenderAsync(PageId, Arg.Any<CancellationToken>()).Returns(ExpectedMarkdown);

        var output = await RunCliGetPlainAsync(editor, renderer, PageId);

        Assert.Contains("# Hello", output);
        Assert.Contains("World", output);
        Assert.DoesNotContain("<!-- buildin:", output);
    }

    private static async Task<string> RunCliGetEditingJsonAsync(IPageEditor editor, string pageId)
    {
        var renderer = Substitute.For<IPageMarkdownRenderer>();
        var (app, _) = CreateGetApp(editor, renderer);

        var sw = new StringWriter();
        var original = Console.Out;
        Console.SetOut(sw);
        try
        {
            await app.RunAsync(["get", pageId, "--editing", "--print", "json"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        return sw.ToString().Trim();
    }

    private static async Task<string> RunCliGetPlainAsync(IPageEditor editor, IPageMarkdownRenderer renderer, string pageId)
    {
        var (app, console) = CreateGetApp(editor, renderer);
        await app.RunAsync(["get", pageId]);
        return console.Output;
    }

    private static async Task<string> RunCliUpdateJsonAsync(IPageEditor editor, string pageId, string revision, string opsJson)
    {
        var (app, _) = CreateUpdateApp(editor);

        var opsFile = Path.GetTempFileName();
        File.WriteAllText(opsFile, opsJson);
        try
        {
            var sw = new StringWriter();
            var original = Console.Out;
            Console.SetOut(sw);
            try
            {
                await app.RunAsync(["update", "--page", pageId, "--revision", revision, "--ops", opsFile, "--print", "json"]);
            }
            finally
            {
                Console.SetOut(original);
            }

            return sw.ToString().Trim();
        }
        finally
        {
            File.Delete(opsFile);
        }
    }

    private static async Task<string> RunMcpGetPageMarkdownAsync(IPageEditor editor, string pageId)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPageEditor>(editor);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddMcpServer().WithTools<GetPageMarkdownToolHandler>();

        await using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<McpServerOptions>>().Value;

        var c2s = new Pipe();
        var s2c = new Pipe();

        var server = McpServer.Create(
            new StreamServerTransport(c2s.Reader.AsStream(), s2c.Writer.AsStream()),
            options,
            sp.GetRequiredService<ILoggerFactory>(),
            sp);

        _ = server.RunAsync();

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(c2s.Writer.AsStream(), s2c.Reader.AsStream()),
            new McpClientOptions(),
            sp.GetRequiredService<ILoggerFactory>());

        try
        {
            var result = await client.CallToolAsync("get_page_markdown", new Dictionary<string, object?>
            {
                ["page_id"] = pageId,
            });

            return result.Content.OfType<TextContentBlock>().First().Text;
        }
        finally
        {
            await client.DisposeAsync();
            await server.DisposeAsync();
            c2s.Writer.Complete();
            c2s.Reader.Complete();
            s2c.Writer.Complete();
            s2c.Reader.Complete();
        }
    }

    private static async Task<string> RunMcpUpdatePageAsync(IPageEditor editor, string pageId, string revision, string opsJson)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPageEditor>(editor);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddMcpServer().WithTools<UpdatePageToolHandler>();

        await using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<McpServerOptions>>().Value;

        var c2s = new Pipe();
        var s2c = new Pipe();

        var server = McpServer.Create(
            new StreamServerTransport(c2s.Reader.AsStream(), s2c.Writer.AsStream()),
            options,
            sp.GetRequiredService<ILoggerFactory>(),
            sp);

        _ = server.RunAsync();

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(c2s.Writer.AsStream(), s2c.Reader.AsStream()),
            new McpClientOptions(),
            sp.GetRequiredService<ILoggerFactory>());

        try
        {
            var result = await client.CallToolAsync("update_page", new Dictionary<string, object?>
            {
                ["page_id"] = pageId,
                ["revision"] = revision,
                ["operations"] = opsJson,
            });

            return result.Content.OfType<TextContentBlock>().First().Text;
        }
        finally
        {
            await client.DisposeAsync();
            await server.DisposeAsync();
            c2s.Writer.Complete();
            c2s.Reader.Complete();
            s2c.Writer.Complete();
            s2c.Reader.Complete();
        }
    }

    private static (CommandApp app, TestConsole console) CreateGetApp(IPageEditor editor, IPageMarkdownRenderer renderer)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(editor);
        services.AddSingleton(renderer);

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

    private static (CommandApp app, TestConsole console) CreateUpdateApp(IPageEditor editor)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(editor);

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
            config.AddCommand<UpdateCommand>("update");
        });

        return (app, testConsole);
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
