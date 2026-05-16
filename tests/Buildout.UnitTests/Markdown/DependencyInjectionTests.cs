using Buildout.Core.Buildin;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DependencyInjection;
using Buildout.Core.Markdown;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown;

public class DependencyInjectionTests
{
    [Fact]
    public void AddBuildoutCore_ResolvesIPageMarkdownRenderer()
    {
        var services = new ServiceCollection();
        services.AddBuildoutCore();
        services.AddSingleton<ILogger<PageMarkdownRenderer>>(NullLogger<PageMarkdownRenderer>.Instance);
        services.AddSingleton<ILogger<DatabaseViewRenderer>>(NullLogger<DatabaseViewRenderer>.Instance);
        services.AddSingleton<IBuildinClient>(Substitute.For<IBuildinClient>());

        using var sp = services.BuildServiceProvider();
        var renderer = sp.GetService<IPageMarkdownRenderer>();

        Assert.NotNull(renderer);
        Assert.IsType<PageMarkdownRenderer>(renderer);
    }
}
