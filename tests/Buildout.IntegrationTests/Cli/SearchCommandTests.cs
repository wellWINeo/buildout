using System.Text;
using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Search;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Buildout.IntegrationTests.Cli;

public sealed class SearchCommandTests
{
    private static (CommandApp app, TestConsole console, ISearchService service) CreateApp(
        bool styledStdout = false)
    {
        var service = Substitute.For<ISearchService>();
        var formatter = new SearchResultFormatter();

        var services = new ServiceCollection();
        services.AddSingleton(service);
        services.AddSingleton<ISearchResultFormatter>(formatter);

        var testConsole = new TestConsole();
        services.AddSingleton<Spectre.Console.IAnsiConsole>(testConsole);

        var caps = new TerminalCapabilities(
            isAnsi: styledStdout,
            isOutputRedirected: !styledStdout,
            noColorEnv: null);
        services.AddSingleton(caps);
        services.AddSingleton<SearchResultStyledRenderer>();

        var registrar = new TypeRegistrar(services);

        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.PropagateExceptions();
            config.AddCommand<SearchCommand>("search");
        });

        return (app, testConsole, service);
    }

    private static List<SearchMatch> MakeMatches(params (string id, string type, string title)[] items)
    {
        return items.Select(i => new SearchMatch
        {
            PageId = i.id,
            ObjectType = Enum.Parse<SearchObjectType>(i.type, ignoreCase: true),
            DisplayTitle = i.title
        }).ToList();
    }

    [Fact]
    public async Task HappyPath_NonTty_StdoutIsFormatterBody()
    {
        var (app, _, service) = CreateApp(styledStdout: false);
        var matches = MakeMatches(
            ("id-1", "page", "First"),
            ("id-2", "database", "Second"));
        service.SearchAsync("test", null, Arg.Any<CancellationToken>())
            .Returns(matches);

        var expected = new SearchResultFormatter().Format(matches);

        var originalOut = Console.Out;
        var sb = new StringBuilder();
        await using var sw = new StringWriter(sb);
        Console.SetOut(sw);
        try
        {
            var exitCode = await app.RunAsync(["search", "test"]);
            Assert.Equal(0, exitCode);
            Assert.Equal(expected, sb.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task HappyPath_Tty_StdoutContainsAnsiAndTitles()
    {
        var console = new TestConsole();
        console.EmitAnsiSequences();
        var renderer = new SearchResultStyledRenderer(console);
        var formatter = new SearchResultFormatter();
        var matches = MakeMatches(
            ("id-1", "page", "Fixture Alpha"),
            ("id-2", "database", "Fixture Beta"));
        var body = formatter.Format(matches);

        renderer.Render(body);

        var output = console.Output;
        Assert.Contains("Fixture Alpha", output);
        Assert.Contains("Fixture Beta", output);
    }

    [Fact]
    public async Task NoMatches_NonTty_StdoutIsEmpty()
    {
        var (app, _, service) = CreateApp(styledStdout: false);
        service.SearchAsync("test", null, Arg.Any<CancellationToken>())
            .Returns(new List<SearchMatch>());

        var originalOut = Console.Out;
        var sb = new StringBuilder();
        await using var sw = new StringWriter(sb);
        Console.SetOut(sw);
        try
        {
            var exitCode = await app.RunAsync(["search", "test"]);
            Assert.Equal(0, exitCode);
            Assert.Equal("", sb.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task NoMatches_Tty_StdoutShowsNoMatchesLine()
    {
        var console = new TestConsole();
        console.EmitAnsiSequences();
        var renderer = new SearchResultStyledRenderer(console);

        renderer.Render("");

        Assert.Contains("No matches.", console.Output);
    }

    [Fact]
    public async Task EmptyQuery_ReturnsExit2()
    {
        var (app, _, service) = CreateApp(styledStdout: true);

        var exitCode = await app.RunAsync(["search", ""]);
        Assert.Equal(2, exitCode);
        await service.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhitespaceQuery_ReturnsExit2()
    {
        var (app, _, service) = CreateApp(styledStdout: true);

        var exitCode = await app.RunAsync(["search", "   "]);
        Assert.Equal(2, exitCode);
        await service.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthFailure_ReturnsExit4()
    {
        var (app, _, service) = CreateApp(styledStdout: false);
        service.SearchAsync("test", null, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SearchMatch>>>(_ => throw new BuildinApiException(
                new ApiError(401, "unauthorized", "Invalid token", string.Empty)));

        var exitCode = await app.RunAsync(["search", "test"]);
        Assert.Equal(4, exitCode);
    }

    [Fact]
    public async Task TransportFailure_ReturnsExit5()
    {
        var (app, _, service) = CreateApp(styledStdout: false);
        service.SearchAsync("test", null, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SearchMatch>>>(_ => throw new BuildinApiException(
                new TransportError(new HttpRequestException("Connection refused"))));

        var exitCode = await app.RunAsync(["search", "test"]);
        Assert.Equal(5, exitCode);
    }

    [Fact]
    public async Task UnexpectedError_ReturnsExit6()
    {
        var (app, _, service) = CreateApp(styledStdout: false);
        service.SearchAsync("test", null, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SearchMatch>>>(_ => throw new BuildinApiException(
                new UnknownError(500, "Internal error")));

        var exitCode = await app.RunAsync(["search", "test"]);
        Assert.Equal(6, exitCode);
    }

    [Fact]
    public async Task ScopedSearch_ReturnsOnlyDescendants()
    {
        var (app, _, service) = CreateApp(styledStdout: false);
        var scopedMatches = MakeMatches(
            ("a-id", "page", "Page A"),
            ("b-id", "page", "Page B"),
            ("c-id", "page", "Page C"));
        service.SearchAsync("q", "a-id", Arg.Any<CancellationToken>())
            .Returns(scopedMatches);

        var expected = new SearchResultFormatter().Format(scopedMatches);

        var originalOut = Console.Out;
        var sb = new StringBuilder();
        await using var sw = new StringWriter(sb);
        Console.SetOut(sw);
        try
        {
            var exitCode = await app.RunAsync(["search", "q", "--page", "a-id"]);
            Assert.Equal(0, exitCode);
            Assert.Equal(expected, sb.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task ScopedSearch_UnrelatedPage_ReturnsEmpty()
    {
        var (app, _, service) = CreateApp(styledStdout: false);
        service.SearchAsync("q", "unrelated-id", Arg.Any<CancellationToken>())
            .Returns(new List<SearchMatch>());

        var originalOut = Console.Out;
        var sb = new StringBuilder();
        await using var sw = new StringWriter(sb);
        Console.SetOut(sw);
        try
        {
            var exitCode = await app.RunAsync(["search", "q", "--page", "unrelated-id"]);
            Assert.Equal(0, exitCode);
            Assert.Equal("", sb.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task ScopedSearch_MissingPage_ReturnsExit3()
    {
        var (app, _, service) = CreateApp(styledStdout: false);
        service.SearchAsync("q", "missing-id", Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SearchMatch>>>(_ => throw new BuildinApiException(
                new ApiError(404, "object_not_found", "Could not find page", string.Empty)));

        var exitCode = await app.RunAsync(["search", "q", "--page", "missing-id"]);
        Assert.Equal(3, exitCode);
    }

    [Fact]
    public async Task ScopedSearch_OutputIsSubsetOfUnscoped()
    {
        var (app, _, service) = CreateApp(styledStdout: false);
        var allMatches = MakeMatches(
            ("a-id", "page", "Page A"),
            ("b-id", "page", "Page B"),
            ("c-id", "page", "Page C"),
            ("d-id", "page", "Page D"));
        var scopedMatches = MakeMatches(
            ("a-id", "page", "Page A"),
            ("b-id", "page", "Page B"),
            ("c-id", "page", "Page C"));
        service.SearchAsync("q", "a-id", Arg.Any<CancellationToken>())
            .Returns(scopedMatches);
        service.SearchAsync("q", null, Arg.Any<CancellationToken>())
            .Returns(allMatches);

        var originalOut = Console.Out;
        var scopedSb = new StringBuilder();
        var unscopedSb = new StringBuilder();

        await using (var sw = new StringWriter(scopedSb))
        {
            Console.SetOut(sw);
            try
            {
                var exitCode = await app.RunAsync(["search", "q", "--page", "a-id"]);
                Assert.Equal(0, exitCode);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        await using (var sw = new StringWriter(unscopedSb))
        {
            Console.SetOut(sw);
            try
            {
                var exitCode = await app.RunAsync(["search", "q"]);
                Assert.Equal(0, exitCode);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        var scopedOutput = scopedSb.ToString();
        var unscopedOutput = unscopedSb.ToString();
        Assert.Contains(scopedOutput, unscopedOutput);
        Assert.NotEqual(scopedOutput, unscopedOutput);
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
