using System.IO.Pipelines;
using Buildout.Core.Buildin;
using Buildout.Core.DependencyInjection;
using Buildout.IntegrationTests.Buildin;
using Buildout.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace Buildout.IntegrationTests.Cross;

[Collection("BuildinWireMock")]
public sealed class CreatePageReadOnlyOnExistingDataTests
{
    private readonly BuildinWireMockFixture _fixture;

    private const string ParentId = "00000000-0000-0000-0000-000000000001";
    private const string NewPageId = "00000000-0000-0000-0000-000000000002";
    private const string Markdown = "Hello world\n\n";

    public CreatePageReadOnlyOnExistingDataTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private void SetupStubs()
    {
        _fixture.Server.Reset();

        // Allowed: parent probe
        BuildinStubs.RegisterPageProbe(_fixture.Server, ParentId, new
        {
            id = ParentId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{ParentId}",
            properties = new { title = new { type = "title", title = Array.Empty<object>() } }
        });

        // Allowed: create page
        BuildinStubs.RegisterCreatePage(_fixture.Server, new
        {
            id = NewPageId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{NewPageId}",
            properties = new { title = new { type = "title", title = new[] { new { type = "text", plain_text = "Hello world" } } } }
        });

        // Allowed: append block children for the new page only
        BuildinStubs.RegisterAppendBlockChildren(_fixture.Server, NewPageId, new
        {
            @object = "list",
            results = Array.Empty<object>(),
            has_more = false,
            next_cursor = (string?)null
        });

        // Fallback: any other request returns 500 (registered last so specific routes win)
        _fixture.Server
            .Given(Request.Create()
                .WithPath(new RegexMatcher("^/v1/.*"))
                .UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { status = 500, code = "unexpected_request", message = "Unexpected endpoint hit during create_page" }));
    }

    [Fact]
    public async Task CreatePage_DoesNotModifyExistingData()
    {
        SetupStubs();

        var client = _fixture.CreateClient();
        var services = new ServiceCollection();
        services.AddBuildoutCore();
        services.AddSingleton(client);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddMcpServer().WithTools<CreatePageToolHandler>();

        await using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var c2s = new Pipe();
        var s2c = new Pipe();

        var mcpServer = McpServer.Create(
            new StreamServerTransport(c2s.Reader.AsStream(), s2c.Writer.AsStream()),
            options,
            sp.GetRequiredService<ILoggerFactory>(),
            sp);

        _ = mcpServer.RunAsync();

        var mcpClient = await McpClient.CreateAsync(
            new StreamClientTransport(c2s.Writer.AsStream(), s2c.Reader.AsStream()),
            new McpClientOptions(),
            sp.GetRequiredService<ILoggerFactory>());

        try
        {
            await mcpClient.CallToolAsync("create_page", new Dictionary<string, object?>
            {
                ["parent_id"] = ParentId,
                ["markdown"] = Markdown,
                ["title"] = "Hello world",
            });
        }
        finally
        {
            await mcpClient.DisposeAsync();
            await mcpServer.DisposeAsync();
            c2s.Writer.Complete();
            c2s.Reader.Complete();
            s2c.Writer.Complete();
            s2c.Reader.Complete();
        }

        var logEntries = _fixture.Server.LogEntries.ToList();

        foreach (var entry in logEntries)
        {
            var method = entry.RequestMessage?.Method?.ToUpperInvariant() ?? "";
            var path = entry.RequestMessage?.Path ?? "";

            bool isForbidden = false;

            // PATCH to existing pages
            if (method == "PATCH" && path.StartsWith("/v1/pages/", StringComparison.Ordinal))
                isForbidden = true;

            // DELETE to any block
            if (method == "DELETE" && path.StartsWith("/v1/blocks/", StringComparison.Ordinal))
                isForbidden = true;

            // PATCH or DELETE to databases
            if (method is "PATCH" or "DELETE" && path.StartsWith("/v1/databases/", StringComparison.Ordinal))
                isForbidden = true;

            // POST to create a database (exact path)
            if (method == "POST" && path == "/v1/databases")
                isForbidden = true;

            // POST to database query
            if (method == "POST" && path.StartsWith("/v1/databases/", StringComparison.Ordinal) && path.EndsWith("/query", StringComparison.Ordinal))
                isForbidden = true;

            // POST to search
            if (method == "POST" && path == "/v1/search")
                isForbidden = true;

            // POST to page search
            if (method == "POST" && path == "/v1/pages/search")
                isForbidden = true;

            // PATCH to /v1/blocks/{id}/children is allowed only for the new page
            if (method == "PATCH" && path.StartsWith("/v1/blocks/", StringComparison.Ordinal) && path.EndsWith("/children", StringComparison.Ordinal))
            {
                var blockId = path.Replace("/v1/blocks/", "").Replace("/children", "");
                if (blockId != NewPageId)
                    isForbidden = true;
            }

            Assert.False(isForbidden, $"Forbidden request: {method} {path}");
        }
    }
}
