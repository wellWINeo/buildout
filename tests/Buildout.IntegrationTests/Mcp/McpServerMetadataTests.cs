using Buildout.IntegrationTests.Buildin;
using Buildout.IntegrationTests.Llm;
using Xunit;

namespace Buildout.IntegrationTests.Mcp;

[Collection("BuildinWireMock")]
public sealed class McpServerMetadataTests
{
    private readonly BuildinWireMockFixture _fixture;

    public McpServerMetadataTests(BuildinWireMockFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task McpServer_AdvertisesNonEmptyServerInstructions()
    {
        await using var harness = await McpSkBridge.CreateHarnessAsync(_fixture);
        Assert.False(string.IsNullOrWhiteSpace(harness.Client.ServerInstructions));
    }
}
