using Buildout.Core.Buildin;
using Buildout.Core.DependencyInjection;
using Buildout.Core.Markdown.Editing;
using Buildout.Core.Markdown.Editing.PatchOperations;
using Buildout.IntegrationTests.Buildin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Buildout.IntegrationTests.Cross;

[Collection("BuildinWireMock")]
public sealed class UpdateReadOnlyOnOtherPagesTests
{
    private readonly BuildinWireMockFixture _fixture;

    private const string PageId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string BlockId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

    public UpdateReadOnlyOnOtherPagesTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private void SetupStubs()
    {
        _fixture.Server.Reset();

        // Allowed: fetch the page
        BuildinStubs.RegisterGetPage(_fixture.Server, new
        {
            id = PageId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{PageId[..8]}",
            properties = new
            {
                title = new
                {
                    type = "title",
                    title = new[] { new { type = "text", plain_text = "Test Page" } }
                }
            }
        });

        // Allowed: fetch block children — one paragraph block containing "Hello"
        BuildinStubs.RegisterGetBlockChildren(_fixture.Server, new
        {
            @object = "list",
            results = new object[]
            {
                new
                {
                    id = BlockId,
                    type = "paragraph",
                    created_time = "2025-01-01T00:00:00Z",
                    has_children = false,
                    data = new { rich_text = new[] { new { type = "text", plain_text = "Hello" } } }
                }
            },
            has_more = false
        });

        // Allowed: update the paragraph block (PATCH /v1/blocks/{id})
        BuildinStubs.RegisterUpdateBlock(_fixture.Server, BlockId, new
        {
            id = BlockId,
            type = "paragraph",
            created_time = "2025-01-15T10:30:00Z",
            has_children = false,
            data = new { rich_text = new[] { new { type = "text", plain_text = "World" } } }
        });
    }

    [Fact]
    public async Task UpdatePage_DoesNotModifyPagesOrDatabases()
    {
        SetupStubs();

        var client = _fixture.CreateClient();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddBuildoutCore();
        services.AddSingleton<IBuildinClient>(client);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        await using var sp = services.BuildServiceProvider();

        var editor = sp.GetRequiredService<IPageEditor>();

        // First fetch the snapshot to get a valid revision token
        var snapshot = await editor.FetchForEditAsync(PageId);

        // Then apply a search_replace operation
        await editor.UpdateAsync(new UpdatePageInput
        {
            PageId = PageId,
            Revision = snapshot.Revision,
            Operations = [new SearchReplaceOperation { OldStr = "Hello", NewStr = "World" }]
        });

        var logEntries = _fixture.Server.LogEntries.ToList();

        foreach (var entry in logEntries)
        {
            var method = entry.RequestMessage?.Method?.ToUpperInvariant() ?? "";
            var path = entry.RequestMessage?.Path ?? "";

            bool isForbidden = false;

            // Must not POST to create a new page
            if (method == "POST" && path == "/v1/pages")
                isForbidden = true;

            // Must not PATCH an existing page
            if (method == "PATCH" && path.StartsWith("/v1/pages/", StringComparison.Ordinal))
                isForbidden = true;

            // Must not POST to create a database
            if (method == "POST" && path == "/v1/databases")
                isForbidden = true;

            // Must not PATCH a database
            if (method == "PATCH" && path.StartsWith("/v1/databases/", StringComparison.Ordinal))
                isForbidden = true;

            Assert.False(isForbidden, $"Forbidden request during update_page: {method} {path}");
        }
    }
}
