using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Core.Buildin;
using Buildout.Core.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

var services = new ServiceCollection();

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

services.AddBuildinClient(configuration);
services.AddBuildoutCore();
services.AddSingleton<Spectre.Console.IAnsiConsole>(_ => Spectre.Console.AnsiConsole.Console);
services.AddSingleton<TerminalCapabilities>();
services.AddSingleton<MarkdownTerminalRenderer>();
services.AddSingleton<SearchResultStyledRenderer>();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.AddCommand<GetCommand>("get");
    config.AddCommand<SearchCommand>("search");
});

await app.RunAsync(args);

internal sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services;
    public TypeRegistrar(IServiceCollection services) => _services = services;
    public void Register(Type service, Type implementation) => _services.AddSingleton(service, implementation);
    public void RegisterInstance(Type service, object implementation) => _services.AddSingleton(service, implementation);
    public void RegisterLazy(Type service, Func<object> factory) => _services.AddSingleton(service, _ => factory());
    public ITypeResolver Build() => new TypeResolver(_services.BuildServiceProvider());
}

internal sealed class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;
    public TypeResolver(IServiceProvider provider) => _provider = provider;
    public object? Resolve(Type? type) => type is null ? null : _provider.GetService(type);
}
