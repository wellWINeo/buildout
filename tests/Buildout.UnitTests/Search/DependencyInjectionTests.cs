using Buildout.Core.Buildin;
using Buildout.Core.DependencyInjection;
using Buildout.Core.Search;
using Buildout.Core.Search.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Search;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddBuildoutCore_RegistersSearchServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IBuildinClient>());
        services.AddSingleton(Substitute.For<ILogger<SearchService>>());
        services.AddSingleton(Substitute.For<ILogger<AncestorScopeFilter>>());
        services.AddBuildoutCore();

        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<ISearchService>());
        Assert.NotNull(sp.GetService<ISearchResultFormatter>());
    }
}
