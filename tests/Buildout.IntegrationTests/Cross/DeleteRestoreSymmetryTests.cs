using Buildout.Cli.Commands;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.PageLifecycle;
using Buildout.IntegrationTests.Buildin;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Spectre.Console.Cli;
using Xunit;

namespace Buildout.IntegrationTests.Cross;

[Collection("BuildinWireMock")]
public sealed class DeleteRestoreSymmetryTests
{
    private readonly BuildinWireMockFixture _fixture;
    private readonly ITestOutputHelper _output;

    private const string PageId = "00000000-0000-0000-0000-000000000001";

    public DeleteRestoreSymmetryTests(BuildinWireMockFixture fixture, ITestOutputHelper output)
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
            config.AddCommand<DeleteCommand>("delete");
            config.AddCommand<RestoreCommand>("restore");
            config.PropagateExceptions();
        });

        return (app, stdout, stderr);
    }

    [Fact]
    public async Task DeleteThenRestore_RoundTrip_BothExit0()
    {
        var lifecycle = Substitute.For<IPageLifecycle>();
        lifecycle.DeleteAsync(PageId, Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome { PageId = PageId, Archived = true, Changed = true });
        lifecycle.RestoreAsync(PageId, Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome { PageId = PageId, Archived = false, Changed = true });

        var (deleteApp, deleteStdout, deleteStderr) = CreateApp(lifecycle);
        var deleteExit = await deleteApp.RunAsync(["delete", PageId]);
        Assert.Equal(0, deleteExit);

        var (restoreApp, restoreStdout, restoreStderr) = CreateApp(lifecycle);
        var restoreExit = await restoreApp.RunAsync(["restore", PageId]);
        Assert.Equal(0, restoreExit);
    }

    [Fact]
    public async Task PrintJson_OutputContainsAllFields()
    {
        var lifecycle = Substitute.For<IPageLifecycle>();
        lifecycle.DeleteAsync(PageId, Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome { PageId = PageId, Archived = true, Changed = true });

        var (app, stdout, stderr) = CreateApp(lifecycle);

        var exitCode = await app.RunAsync(["delete", PageId, "--print", "json"]);

        Assert.Equal(0, exitCode);
        var output = stdout.ToString();
        Assert.Contains(PageId, output);
        Assert.Contains("\"archived\":true", output);
        Assert.Contains("\"changed\":true", output);
    }

    [Fact]
    public async Task ErrorClass_MatchesBetweenAttempts()
    {
        var lifecycle = Substitute.For<IPageLifecycle>();
        lifecycle.DeleteAsync(PageId, Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome
            {
                PageId = PageId,
                Changed = false,
                FailureClass = FailureClass.NotFound,
                UnderlyingException = new InvalidOperationException("not found"),
            });

        var (app, stdout, stderr) = CreateApp(lifecycle);

        var exitCode = await app.RunAsync(["delete", PageId]);

        Assert.Equal(3, exitCode);
        Assert.Contains("NotFound", stderr.ToString());
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
