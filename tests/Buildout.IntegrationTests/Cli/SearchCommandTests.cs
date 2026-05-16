using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Buildout.IntegrationTests.Cli;

public sealed class SearchCommandTests
{
    private static (CommandApp app, TestConsole console) CreateApp(
        ISearchService service,
        bool styledStdout = false)
    {
        var formatter = new SearchResultFormatter();

        var services = new ServiceCollection();
        services.AddLogging();
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

        // Pin IAnsiConsole so the TypeRegistrar ignores Spectre's attempt to override it
        var pinnedTypes = new HashSet<Type> { typeof(Spectre.Console.IAnsiConsole) };
        var registrar = new TypeRegistrar(services, pinnedTypes);

        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.PropagateExceptions();
            config.AddCommand<SearchCommand>("search");
        });

        return (app, testConsole);
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
        var service = Substitute.For<ISearchService>();
        var matches = MakeMatches(
            ("id-1", "page", "First"),
            ("id-2", "database", "Second"));
        service.SearchAsync("test", null, Arg.Any<CancellationToken>())
            .Returns(matches);

        var expected = new SearchResultFormatter().Format(matches);

        var (app, console) = CreateApp(service, styledStdout: false);
        var exitCode = await app.RunAsync(["search", "test"]);
        Assert.Equal(0, exitCode);
        Assert.Equal(expected, console.Output);
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
        var service = Substitute.For<ISearchService>();
        service.SearchAsync("test", null, Arg.Any<CancellationToken>())
            .Returns(new List<SearchMatch>());

        var (app, console) = CreateApp(service, styledStdout: false);
        var exitCode = await app.RunAsync(["search", "test"]);
        Assert.Equal(0, exitCode);
        Assert.Equal("", console.Output);
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
        var service = Substitute.For<ISearchService>();
        var (app, _) = CreateApp(service, styledStdout: true);

        var exitCode = await app.RunAsync(["search", ""]);
        Assert.Equal(2, exitCode);
        await service.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhitespaceQuery_ReturnsExit2()
    {
        var service = Substitute.For<ISearchService>();
        var (app, _) = CreateApp(service, styledStdout: true);

        var exitCode = await app.RunAsync(["search", "   "]);
        Assert.Equal(2, exitCode);
        await service.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthFailure_ReturnsExit4()
    {
        var service = Substitute.For<ISearchService>();
        service.SearchAsync("test", null, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SearchMatch>>>(_ => throw new BuildinApiException(
                new ApiError(401, "unauthorized", "Invalid token", string.Empty)));

        var (app, _) = CreateApp(service, styledStdout: false);
        var exitCode = await app.RunAsync(["search", "test"]);
        Assert.Equal(4, exitCode);
    }

    [Fact]
    public async Task TransportFailure_ReturnsExit5()
    {
        var service = Substitute.For<ISearchService>();
        service.SearchAsync("test", null, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SearchMatch>>>(_ => throw new BuildinApiException(
                new TransportError(new HttpRequestException("Connection refused"))));

        var (app, _) = CreateApp(service, styledStdout: false);
        var exitCode = await app.RunAsync(["search", "test"]);
        Assert.Equal(5, exitCode);
    }

    [Fact]
    public async Task UnexpectedError_ReturnsExit6()
    {
        var service = Substitute.For<ISearchService>();
        service.SearchAsync("test", null, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SearchMatch>>>(_ => throw new BuildinApiException(
                new UnknownError(500, "Internal error")));

        var (app, _) = CreateApp(service, styledStdout: false);
        var exitCode = await app.RunAsync(["search", "test"]);
        Assert.Equal(6, exitCode);
    }

    [Fact]
    public async Task ScopedSearch_ReturnsOnlyDescendants()
    {
        var service = Substitute.For<ISearchService>();
        var scopedMatches = MakeMatches(
            ("a-id", "page", "Page A"),
            ("b-id", "page", "Page B"),
            ("c-id", "page", "Page C"));
        service.SearchAsync("q", "a-id", Arg.Any<CancellationToken>())
            .Returns(scopedMatches);

        var expected = new SearchResultFormatter().Format(scopedMatches);

        var (app, console) = CreateApp(service, styledStdout: false);
        var exitCode = await app.RunAsync(["search", "q", "--page", "a-id"]);
        Assert.Equal(0, exitCode);
        Assert.Equal(expected, console.Output);
    }

    [Fact]
    public async Task ScopedSearch_UnrelatedPage_ReturnsEmpty()
    {
        var service = Substitute.For<ISearchService>();
        service.SearchAsync("q", "unrelated-id", Arg.Any<CancellationToken>())
            .Returns(new List<SearchMatch>());

        var (app, console) = CreateApp(service, styledStdout: false);
        var exitCode = await app.RunAsync(["search", "q", "--page", "unrelated-id"]);
        Assert.Equal(0, exitCode);
        Assert.Equal("", console.Output);
    }

    [Fact]
    public async Task ScopedSearch_MissingPage_ReturnsExit3()
    {
        var service = Substitute.For<ISearchService>();
        service.SearchAsync("q", "missing-id", Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SearchMatch>>>(_ => throw new BuildinApiException(
                new ApiError(404, "object_not_found", "Could not find page", string.Empty)));

        var (app, _) = CreateApp(service, styledStdout: false);
        var exitCode = await app.RunAsync(["search", "q", "--page", "missing-id"]);
        Assert.Equal(3, exitCode);
    }

    [Fact]
    public async Task ScopedSearch_OutputIsSubsetOfUnscoped()
    {
        var service = Substitute.For<ISearchService>();
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

        var (scopedApp, scopedConsole) = CreateApp(service, styledStdout: false);
        var scopedExitCode = await scopedApp.RunAsync(["search", "q", "--page", "a-id"]);
        Assert.Equal(0, scopedExitCode);

        var (unscopedApp, unscopedConsole) = CreateApp(service, styledStdout: false);
        var unscopedExitCode = await unscopedApp.RunAsync(["search", "q"]);
        Assert.Equal(0, unscopedExitCode);

        var scopedOutput = scopedConsole.Output;
        var unscopedOutput = unscopedConsole.Output;
        Assert.Contains(scopedOutput, unscopedOutput);
        Assert.NotEqual(scopedOutput, unscopedOutput);
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
