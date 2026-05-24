using Buildout.Core.Buildin;
using Buildout.Core.Caching;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DependencyInjection;
using Buildout.Core.Markdown;
using Microsoft.Extensions.Configuration;
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
        var configuration = new ConfigurationBuilder().Build();
        services.AddLogging();
        services.AddBuildoutCore(configuration);
        services.AddSingleton<IBuildinClient>(Substitute.For<IBuildinClient>());

        using var sp = services.BuildServiceProvider();
        var renderer = sp.GetService<IPageMarkdownRenderer>();

        Assert.NotNull(renderer);
        Assert.IsType<PageMarkdownRenderer>(renderer);
    }

    /// <summary>
    /// Verifies that AddBuildoutCore works correctly alongside AddLogging(), which is how
    /// the CLI registers services. IPageContentProvider uses ILoggerFactory internally and
    /// requires logging to be registered in the container.
    /// </summary>
    [Fact]
    public void AddBuildoutCore_WithLogging_ResolvesIPageContentProvider()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddLogging();
        services.AddBuildoutCore(configuration);
        services.AddSingleton<IBuildinClient>(Substitute.For<IBuildinClient>());

        using var sp = services.BuildServiceProvider();
        var provider = sp.GetRequiredService<IPageContentProvider>();

        Assert.NotNull(provider);
    }
}
