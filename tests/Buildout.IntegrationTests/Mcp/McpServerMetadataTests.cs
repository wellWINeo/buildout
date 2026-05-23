using Buildout.IntegrationTests.Buildin;
using Buildout.IntegrationTests.Llm;
using Buildout.Mcp.Prompts;
using ModelContextProtocol.Protocol;
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

    [Fact]
    public async Task McpServer_ServerInstructions_MatchEmbeddedResource()
    {
        await using var harness = await McpSkBridge.CreateHarnessAsync(_fixture);
        var expected = PromptResourceLoader.Load("server-instructions");
        Assert.Equal(expected, harness.Client.ServerInstructions);
    }

    [Fact]
    public async Task McpSubprocess_ReadResource_ReturnsParagraphContent()
    {
        const string pageId = "33333333-3333-3333-3333-333333333333";

        _fixture.Reset();

        BuildinStubs.RegisterGetPage(_fixture.Server, new
        {
            id = pageId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = "https://api.buildin.ai/pages/33333333",
            properties = new
            {
                title = new { title = new[] { new { type = "text", plain_text = "Quarterly Revenue Report" } } }
            }
        });

        BuildinStubs.RegisterGetBlockChildren(_fixture.Server, new
        {
            @object = "list",
            results = new object[]
            {
                new
                {
                    id = "44444444-4444-4444-4444-444444444441",
                    type = "paragraph",
                    created_time = "2025-01-01T00:00:00Z",
                    has_children = false,
                    data = new
                    {
                        rich_text = new object[]
                        {
                            new { type = "text", plain_text = "Total revenue for Q3 2025 was " },
                            new { type = "text", plain_text = "$4.2 million", annotations = new { bold = true } },
                            new { type = "text", plain_text = ", up 12% from Q2." }
                        }
                    }
                }
            },
            has_more = false
        });

        await using var harness = await McpSkBridge.CreateHarnessAsync(_fixture);

        var result = await harness.Client.ReadResourceAsync($"buildin://{pageId}");
        var text = result.Contents.OfType<TextResourceContents>().First().Text;

        Assert.Contains("4.2", text);
    }
}
