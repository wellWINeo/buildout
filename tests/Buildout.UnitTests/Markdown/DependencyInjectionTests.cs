using Buildout.Core.Buildin;
using Buildout.Core.DependencyInjection;
using Buildout.Core.Markdown;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<IBuildinClient>(Substitute.For<IBuildinClient>());

        using var sp = services.BuildServiceProvider();
        var renderer = sp.GetService<IPageMarkdownRenderer>();

        Assert.NotNull(renderer);
        Assert.IsType<PageMarkdownRenderer>(renderer);
    }
}
