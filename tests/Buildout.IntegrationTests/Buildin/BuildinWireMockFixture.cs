using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Authentication;
using Microsoft.Extensions.Logging;
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
        var httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        var authProvider = new BotTokenAuthenticationProvider("test-token");
        var options = Options.Create(new BuildinClientOptions());
        var logger = LoggerFactory.Create(_ => { }).CreateLogger<BotBuildinClient>();
        return new BotBuildinClient(httpClient, authProvider, options, logger);
    }

    public void Reset()
    {
        Server.Reset();
        BuildinStubs.RegisterAll(Server);
    }

    public void Dispose()
    {
        Server.Dispose();
    }
}

[CollectionDefinition("BuildinWireMock")]
public class BuildinWireMockDefinition : ICollectionFixture<BuildinWireMockFixture>;
