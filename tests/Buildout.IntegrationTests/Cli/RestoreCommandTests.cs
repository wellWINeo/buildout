using Buildout.Cli.Commands;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.PageLifecycle;
using Buildout.IntegrationTests.Buildin;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Spectre.Console.Cli;
using Xunit;

namespace Buildout.IntegrationTests.Cli;

[Collection("BuildinWireMock")]
public sealed class RestoreCommandTests
{
    private readonly BuildinWireMockFixture _fixture;
    private readonly ITestOutputHelper _output;

    private const string PageId = "00000000-0000-0000-0000-000000000001";

    public RestoreCommandTests(BuildinWireMockFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _fixture.Reset();
    }

    private static (CommandApp app, StringWriter stdout, StringWriter stderr) CreateApp(IPageLifecycle lifecycle)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPageLifecycle>(lifecycle);

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.AddCommand<RestoreCommand>("restore");
            config.PropagateExceptions();
        });

        return (app, stdout, stderr);
    }

    [Fact]
    public async Task HappyPath_StateChange_Exits0()
    {
        var lifecycle = Substitute.For<IPageLifecycle>();
        lifecycle.RestoreAsync(PageId, Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome
            {
                PageId = PageId,
                Archived = false,
                Changed = true,
            });

        var (app, stdout, stderr) = CreateApp(lifecycle);

        var exitCode = await app.RunAsync(["restore", PageId]);

        Assert.Equal(0, exitCode);
        Assert.Contains($"Restored page {PageId}: archived=false (changed=true)", stdout.ToString());
    }

    [Fact]
    public async Task HappyPath_NoOp_Exits0()
    {
        var lifecycle = Substitute.For<IPageLifecycle>();
        lifecycle.RestoreAsync(PageId, Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome
            {
                PageId = PageId,
                Archived = false,
                Changed = false,
            });

        var (app, stdout, stderr) = CreateApp(lifecycle);

        var exitCode = await app.RunAsync(["restore", PageId]);

        Assert.Equal(0, exitCode);
        Assert.Contains($"Restored page {PageId}: archived=false (changed=false, no-op)", stdout.ToString());
    }

    [Fact]
    public async Task PrintJson_Success()
    {
        var lifecycle = Substitute.For<IPageLifecycle>();
        lifecycle.RestoreAsync(PageId, Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome
            {
                PageId = PageId,
                Archived = false,
                Changed = true,
            });

        var (app, stdout, stderr) = CreateApp(lifecycle);

        var exitCode = await app.RunAsync(["restore", PageId, "--print", "json"]);

        Assert.Equal(0, exitCode);
        var output = stdout.ToString();
        Assert.Contains(PageId, output);
        Assert.Contains("\"archived\":false", output);
        Assert.Contains("\"changed\":true", output);
    }

    [Fact]
    public async Task PrintJson_NoOp()
    {
        var lifecycle = Substitute.For<IPageLifecycle>();
        lifecycle.RestoreAsync(PageId, Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome
            {
                PageId = PageId,
                Archived = false,
                Changed = false,
            });

        var (app, stdout, stderr) = CreateApp(lifecycle);

        var exitCode = await app.RunAsync(["restore", PageId, "--print", "json"]);

        Assert.Equal(0, exitCode);
        var output = stdout.ToString();
        Assert.Contains("\"changed\":false", output);
    }

    [Fact]
    public async Task NotFound_Exits3()
    {
        var lifecycle = Substitute.For<IPageLifecycle>();
        lifecycle.RestoreAsync(PageId, Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome
            {
                PageId = PageId,
                Changed = false,
                FailureClass = FailureClass.NotFound,
                UnderlyingException = new InvalidOperationException("not found"),
            });

        var (app, stdout, stderr) = CreateApp(lifecycle);

        var exitCode = await app.RunAsync(["restore", PageId]);

        Assert.Equal(3, exitCode);
        Assert.Contains("NotFound", stderr.ToString());
    }

    [Fact]
    public async Task AuthFailure_Exits4()
    {
        var lifecycle = Substitute.For<IPageLifecycle>();
        lifecycle.RestoreAsync(PageId, Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome
            {
                PageId = PageId,
                Changed = false,
                FailureClass = FailureClass.Auth,
                UnderlyingException = new UnauthorizedAccessException("invalid token"),
            });

        var (app, stdout, stderr) = CreateApp(lifecycle);

        var exitCode = await app.RunAsync(["restore", PageId]);

        Assert.Equal(4, exitCode);
        Assert.Contains("Auth", stderr.ToString());
    }

    [Fact]
    public async Task TransportError_Exits5()
    {
        var lifecycle = Substitute.For<IPageLifecycle>();
        lifecycle.RestoreAsync(PageId, Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome
            {
                PageId = PageId,
                Changed = false,
                FailureClass = FailureClass.Transport,
                UnderlyingException = new HttpRequestException("connection refused"),
            });

        var (app, stdout, stderr) = CreateApp(lifecycle);

        var exitCode = await app.RunAsync(["restore", PageId]);

        Assert.Equal(5, exitCode);
        Assert.Contains("Transport", stderr.ToString());
    }

    [Fact]
    public async Task UnexpectedError_Exits6()
    {
        var lifecycle = Substitute.For<IPageLifecycle>();
        lifecycle.RestoreAsync(PageId, Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome
            {
                PageId = PageId,
                Changed = false,
                FailureClass = FailureClass.Unexpected,
                UnderlyingException = new InvalidOperationException("something went wrong"),
            });

        var (app, stdout, stderr) = CreateApp(lifecycle);

        var exitCode = await app.RunAsync(["restore", PageId]);

        Assert.Equal(6, exitCode);
        Assert.Contains("Unexpected", stderr.ToString());
    }

    private sealed class TypeRegistrar : ITypeRegistrar
    {
        private readonly IServiceCollection _services;

        public TypeRegistrar(IServiceCollection services)
        {
            _services = services;
        }

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
