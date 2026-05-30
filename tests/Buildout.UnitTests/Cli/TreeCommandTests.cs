using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.PageTree;
using Buildout.Core.PageTree.Errors;
using Buildout.Core.PageTree.Rendering;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Spectre.Console.Cli;
using Xunit;

namespace Buildout.UnitTests.Cli;

public sealed class TreeCommandTests
{
    private readonly IPageTreeService _service = Substitute.For<IPageTreeService>();
    private readonly ITreeRenderer _asciiRenderer = Substitute.For<ITreeRenderer>();
    private readonly ITreeRenderer _jsonRenderer = Substitute.For<ITreeRenderer>();

    private CommandApp CreateApp()
    {
        _asciiRenderer.Format.Returns(TreeFormat.Ascii);
        _jsonRenderer.Format.Returns(TreeFormat.Json);

        var rendererDict = new Dictionary<TreeFormat, ITreeRenderer>
        {
            [TreeFormat.Ascii] = _asciiRenderer,
            [TreeFormat.Json] = _jsonRenderer,
        };

        var services = new ServiceCollection();
        services.AddSingleton(_service);
        services.AddSingleton<IReadOnlyDictionary<TreeFormat, ITreeRenderer>>(rendererDict);
        services.AddSingleton<Spectre.Console.IAnsiConsole>(_ => Spectre.Console.AnsiConsole.Console);
        services.AddLogging();

        var pinnedTypes = new HashSet<Type> { typeof(Spectre.Console.IAnsiConsole) };
        var registrar = new TypeRegistrar(services, pinnedTypes);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.PropagateExceptions();
            config.AddCommand<TreeCommand>("tree");
        });
        return app;
    }

    private static TreeNode EmptyRoot() => new("Root", "https://x.com", []);

    [Fact]
    public async Task SuccessfulRun_ReturnsExitCode0()
    {
        _service.BuildAsync("page-id", 3, Arg.Any<CancellationToken>()).Returns(EmptyRoot());
        _asciiRenderer.Render(Arg.Any<TreeNode>()).Returns("rendered output");

        var app = CreateApp();
        var (exitCode, _, _) = await RunAndCaptureAsync(app, ["tree", "page-id"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task SuccessfulRun_OutputGoesToStdout()
    {
        _service.BuildAsync("page-id", 3, Arg.Any<CancellationToken>()).Returns(EmptyRoot());
        _asciiRenderer.Render(Arg.Any<TreeNode>()).Returns("the tree output");

        var app = CreateApp();
        var (_, stdout, _) = await RunAndCaptureAsync(app, ["tree", "page-id"]);

        Assert.Contains("the tree output", stdout);
    }

    [Fact]
    public async Task DepthZero_ReturnsExitCode2()
    {
        var app = CreateApp();
        var (exitCode, _, stderr) = await RunAndCaptureAsync(app, ["tree", "page-id", "--depth", "0"]);

        Assert.Equal(2, exitCode);
        Assert.Contains("depth must be between 1 and 7", stderr);
        Assert.Contains("0", stderr);
    }

    [Fact]
    public async Task DepthEight_ReturnsExitCode2()
    {
        var app = CreateApp();
        var (exitCode, _, stderr) = await RunAndCaptureAsync(app, ["tree", "page-id", "--depth", "8"]);

        Assert.Equal(2, exitCode);
        Assert.Contains("depth must be between 1 and 7", stderr);
        Assert.Contains("8", stderr);
    }

    [Fact]
    public async Task DepthZero_ErrorMessageContainsRange()
    {
        var app = CreateApp();
        var (_, _, stderr) = await RunAndCaptureAsync(app, ["tree", "page-id", "--depth", "0"]);

        Assert.Contains("1", stderr);
        Assert.Contains("7", stderr);
    }

    [Fact]
    public async Task OmittingDepth_BehavesIdenticalToDepth3()
    {
        _service.BuildAsync("page-id", 3, Arg.Any<CancellationToken>()).Returns(EmptyRoot());
        _asciiRenderer.Render(Arg.Any<TreeNode>()).Returns("output");

        var app = CreateApp();
        var (exitCode, _, _) = await RunAndCaptureAsync(app, ["tree", "page-id"]);

        Assert.Equal(0, exitCode);
        await _service.Received(1).BuildAsync("page-id", 3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Depth7_ReturnsExitCode0()
    {
        _service.BuildAsync("page-id", 7, Arg.Any<CancellationToken>()).Returns(EmptyRoot());
        _asciiRenderer.Render(Arg.Any<TreeNode>()).Returns("output");

        var app = CreateApp();
        var (exitCode, _, _) = await RunAndCaptureAsync(app, ["tree", "page-id", "--depth", "7"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RootNotFound_ReturnsExitCode3()
    {
        _service.BuildAsync("page-id", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new TreeRootNotFoundException("page-id", new BuildinApiException(new ApiError(404, "not_found", "Not found", null))));

        var app = CreateApp();
        var (exitCode, _, _) = await RunAndCaptureAsync(app, ["tree", "page-id"]);

        Assert.Equal(3, exitCode);
    }

    [Fact]
    public async Task AuthFailure_ReturnsExitCode4()
    {
        _service.BuildAsync("page-id", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new ApiError(401, "unauthorized", "Unauthorized", null)));

        var app = CreateApp();
        var (exitCode, _, _) = await RunAndCaptureAsync(app, ["tree", "page-id"]);

        Assert.Equal(4, exitCode);
    }

    [Fact]
    public async Task TransportFailure_ReturnsExitCode5()
    {
        _service.BuildAsync("page-id", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new TransportError(new HttpRequestException("Connection refused"))));

        var app = CreateApp();
        var (exitCode, _, _) = await RunAndCaptureAsync(app, ["tree", "page-id"]);

        Assert.Equal(5, exitCode);
    }

    [Fact]
    public async Task UnexpectedBuildinError_ReturnsExitCode6()
    {
        _service.BuildAsync("page-id", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new ApiError(500, "server_error", "Internal server error", null)));

        var app = CreateApp();
        var (exitCode, _, _) = await RunAndCaptureAsync(app, ["tree", "page-id"]);

        Assert.Equal(6, exitCode);
    }

    [Fact]
    public async Task CycleDetected_ReturnsExitCode7()
    {
        _service.BuildAsync("page-id", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new TreeCycleDetectedException("cycle-node-id"));

        var app = CreateApp();
        var (exitCode, _, _) = await RunAndCaptureAsync(app, ["tree", "page-id"]);

        Assert.Equal(7, exitCode);
    }

    [Fact]
    public async Task ErrorMessages_GoToStderr()
    {
        _service.BuildAsync("page-id", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new TreeRootNotFoundException("page-id", new BuildinApiException(new ApiError(404, "not_found", "Not found", null))));

        var app = CreateApp();
        var (_, stdout, stderr) = await RunAndCaptureAsync(app, ["tree", "page-id"]);

        Assert.NotEmpty(stderr);
        Assert.DoesNotContain("not found", stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DefaultFormat_UsesAsciiRenderer()
    {
        _service.BuildAsync("page-id", 3, Arg.Any<CancellationToken>()).Returns(EmptyRoot());
        _asciiRenderer.Render(Arg.Any<TreeNode>()).Returns("ascii output");

        var app = CreateApp();
        await RunAndCaptureAsync(app, ["tree", "page-id"]);

        _asciiRenderer.Received(1).Render(Arg.Any<TreeNode>());
        _jsonRenderer.DidNotReceive().Render(Arg.Any<TreeNode>());
    }

    [Fact]
    public async Task FormatJson_UsesJsonRenderer()
    {
        _service.BuildAsync("page-id", 3, Arg.Any<CancellationToken>()).Returns(EmptyRoot());
        _jsonRenderer.Render(Arg.Any<TreeNode>()).Returns("{\"name\":\"Root\"}");

        var app = CreateApp();
        await RunAndCaptureAsync(app, ["tree", "page-id", "--format", "json"]);

        _jsonRenderer.Received(1).Render(Arg.Any<TreeNode>());
        _asciiRenderer.DidNotReceive().Render(Arg.Any<TreeNode>());
    }

    [Fact]
    public async Task DepthZero_ErrorMessageIsExact()
    {
        var app = CreateApp();
        var (_, _, stderr) = await RunAndCaptureAsync(app, ["tree", "page-id", "--depth", "0"]);

        Assert.Contains("depth must be between 1 and 7 (inclusive); got 0", stderr);
    }

    [Fact]
    public async Task DepthEight_ErrorMessageIsExact()
    {
        var app = CreateApp();
        var (_, _, stderr) = await RunAndCaptureAsync(app, ["tree", "page-id", "--depth", "8"]);

        Assert.Contains("depth must be between 1 and 7 (inclusive); got 8", stderr);
    }

    [Fact]
    public async Task FormatAsciiExplicit_UsesAsciiRenderer()
    {
        _service.BuildAsync("page-id", 3, Arg.Any<CancellationToken>()).Returns(EmptyRoot());
        _asciiRenderer.Render(Arg.Any<TreeNode>()).Returns("ascii output");

        var app = CreateApp();
        await RunAndCaptureAsync(app, ["tree", "page-id", "--format", "ascii"]);

        _asciiRenderer.Received(1).Render(Arg.Any<TreeNode>());
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAndCaptureAsync(CommandApp app, string[] args)
    {
        var stdoutSw = new StringWriter();
        var stderrSw = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        Console.SetOut(stdoutSw);
        Console.SetError(stderrSw);
        int exitCode;
        try
        {
            exitCode = await app.RunAsync(args);
        }
        catch (Exception)
        {
            exitCode = 1;
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }

        return (exitCode, stdoutSw.ToString(), stderrSw.ToString());
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
