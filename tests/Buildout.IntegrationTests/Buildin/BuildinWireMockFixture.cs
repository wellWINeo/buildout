using Buildout.Core.Buildin;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WireMock.Server;
using Xunit;

namespace Buildout.IntegrationTests.Buildin;

public sealed class BuildinWireMockFixture : IDisposable
{
    public WireMockServer Server { get; }

    public string BaseUrl => Server.Urls[0];

    public BuildinWireMockFixture()
    {
        Server = WireMockServer.Start();
        BuildinStubs.RegisterAll(Server);
    }

    public IBuildinClient CreateClient()
    {
        var options = Options.Create(new BuildinClientOptions
        {
            BaseUrl = new Uri($"{BaseUrl}/"),
            AccessToken = "test-token"
        });
        var httpClient = new HttpClient { BaseAddress = options.Value.BaseUrl };
        var tokenResolver = new AccessTokenResolver(options, NullLogger<AccessTokenResolver>.Instance);
        return new BuildinClient(httpClient, tokenResolver, options, NullLogger<BuildinClient>.Instance);
    }

    public void Reset()
    {
        Server.Reset();
        BuildinStubs.RegisterAll(Server);
    }

    public IReadOnlyList<string> RequestPaths() => BuildinStubs.RequestJournal(Server);

    public void AssertV2Only()
    {
        Assert.DoesNotContain(RequestPaths(), path => path.StartsWith("/v1", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        Server.Dispose();
    }
}

[CollectionDefinition("BuildinWireMock")]
public class BuildinWireMockDefinition : ICollectionFixture<BuildinWireMockFixture>;
