using System.IO.Pipelines;
using System.Text.Json.Nodes;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;
using Annotations = Buildout.Core.Buildin.Models.Annotations;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.DependencyInjection;
using Buildout.Mcp.Resources;
using Buildout.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Xunit;

namespace Buildout.IntegrationTests.Llm;

public sealed class PageReadingLlmTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task LlmCanAnswerQuestionsAboutRenderedPage()
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        var markdown = """
                        # Quarterly Revenue Report

                        Total revenue for Q3 2025 was **$4.2 million**, up 12% from Q2.
                        The largest contributor was the Enterprise segment at $2.8 million.
                        """;

        var prompt = $"""
                      You are reading a rendered Markdown page. Answer the following question
                      based ONLY on the content below.

                      --- PAGE START ---
                      {markdown}
                      --- PAGE END ---

                      Question: What was the total revenue for Q3 2025, and which segment contributed the most?
                      Answer in a single sentence.
                      """;

        var client = new AnthropicClient(new APIAuthentication(apiKey));

        var parameters = new MessageParameters
        {
            Model = "claude-haiku-4-5-20250415",
            MaxTokens = 200,
            Messages = [new Message { Role = RoleType.User, Content = [new TextContent { Text = prompt }] }],
        };

        var response = await client.Messages.GetClaudeMessageAsync(parameters);

        Assert.NotEmpty(response.Content);
        var text = string.Join("", response.Content.OfType<TextContent>().Select(c => c.Text));
        _output.WriteLine($"LLM response: {text}");

        Assert.Contains("4.2", text);
        Assert.Contains("Enterprise", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LlmCanFindAndReadPage()
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        var buildinClient = Substitute.For<IBuildinClient>();

        buildinClient.SearchPagesAsync(
                Arg.Is<PageSearchRequest>(r => r.Query!.Contains("quarterly")),
                Arg.Any<CancellationToken>())
            .Returns(new PageSearchResults
            {
                Results =
                [
                    new()
                    {
                        Id = "q3-page-id",
                        Title = [new RichText { Type = "text", Content = "Q3 Revenue Report" }],
                        Archived = false,
                        ObjectType = "page"
                    },
                    new()
                    {
                        Id = "mkt-page-id",
                        Title = [new RichText { Type = "text", Content = "Marketing Plan" }],
                        Archived = false,
                        ObjectType = "page"
                    }
                ],
                HasMore = false
            });

        buildinClient.GetPageAsync("q3-page-id", Arg.Any<CancellationToken>())
            .Returns(new Page
            {
                Id = "q3-page-id",
                Title = [new RichText { Type = "text", Content = "Quarterly Revenue Report" }],
                Archived = false,
                ObjectType = "page"
            });

        buildinClient.GetBlockChildrenAsync("q3-page-id", Arg.Any<BlockChildrenQuery?>(),
                Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block>
            {
                Results =
                [
                    new ParagraphBlock
                    {
                        Id = "block-1",
                        RichTextContent =
                        [
                            new RichText { Type = "text", Content = "Total revenue for Q3 2025 was " },
                            new RichText
                            {
                                Type = "text", Content = "$4.2 million",
                                Annotations = new Annotations { Bold = true }
                            },
                            new RichText
                            {
                                Type = "text",
                                Content = ", up 12% from Q2. The largest contributor was the Enterprise segment at $2.8 million."
                            }
                        ]
                    }
                ],
                HasMore = false
            });

        var services = new ServiceCollection();
        services.AddSingleton<IBuildinClient>(buildinClient);
        services.AddBuildoutCore();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.AddMcpServer().WithResources<PageResourceHandler>().WithTools<SearchToolHandler>();

        var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<McpServerOptions>>().Value;

        var c2s = new Pipe();
        var s2c = new Pipe();

        var server = McpServer.Create(
            new StreamServerTransport(c2s.Reader.AsStream(), s2c.Writer.AsStream()),
            options,
            sp.GetRequiredService<ILoggerFactory>(),
            sp);

        _ = server.RunAsync();

        var mcpClient = await McpClient.CreateAsync(
            new StreamClientTransport(c2s.Writer.AsStream(), s2c.Reader.AsStream()),
            new McpClientOptions(),
            sp.GetRequiredService<ILoggerFactory>());

        var tools = new List<Anthropic.SDK.Common.Tool>
        {
            new Function("search",
                "Search buildin pages by query. Returns one match per line, tab-separated: <page_id>\\t<object_type>\\t<title>. Use read_buildin_page with the page_id to read a match.",
                JsonNode.Parse("{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\",\"description\":\"Non-empty search query.\"},\"pageId\":{\"type\":\"string\",\"description\":\"Optional buildin page UUID. When set, restricts results to descendants of this page.\"}},\"required\":[\"query\"]}")!),
            new Function("read_buildin_page",
                "Read a Buildin page by page ID and return its rendered Markdown content. Use the page_id from search results.",
                JsonNode.Parse("{\"type\":\"object\",\"properties\":{\"pageId\":{\"type\":\"string\",\"description\":\"The Buildin page ID\"}},\"required\":[\"pageId\"]}")!)
        };

        var anthropicClient = new AnthropicClient(new APIAuthentication(apiKey));

        var messages = new List<Message>
        {
            new()
            {
                Role = RoleType.User,
                Content = [new TextContent { Text = "Which page describes Q3 revenue, and what was the total?" }]
            }
        };

        var searchInvoked = false;
        var readQ3Page = false;
        var finalText = "";

        for (var i = 0; i < 10; i++)
        {
            var parameters = new MessageParameters
            {
                Model = "claude-haiku-4-5-20250415",
                MaxTokens = 1024,
                Tools = tools,
                Messages = messages,
            };

            var response = await anthropicClient.Messages.GetClaudeMessageAsync(parameters);

            var toolUses = response.Content.OfType<ToolUseContent>().ToList();
            if (toolUses.Count == 0)
            {
                finalText = string.Join("", response.Content.OfType<TextContent>().Select(c => c.Text));
                break;
            }

            messages.Add(response.Message);

            var toolResults = new List<ContentBase>();
            foreach (var toolUse in toolUses)
            {
                string result;
                if (toolUse.Name == "search")
                {
                    searchInvoked = true;
                    var query = toolUse.Input["query"]?.ToString() ?? "";
                    var mcpResult = await mcpClient.CallToolAsync("search",
                        new Dictionary<string, object?> { ["query"] = query });
                    result = mcpResult.Content.OfType<TextContentBlock>().First().Text;
                }
                else if (toolUse.Name == "read_buildin_page")
                {
                    var pageId = toolUse.Input["pageId"]?.ToString() ?? "";
                    if (pageId == "q3-page-id")
                        readQ3Page = true;
                    var resourceResult = await mcpClient.ReadResourceAsync($"buildin://{pageId}");
                    result = resourceResult.Contents.OfType<TextResourceContents>().First().Text;
                }
                else
                {
                    result = $"Unknown tool: {toolUse.Name}";
                }

                toolResults.Add(new ToolResultContent
                {
                    ToolUseId = toolUse.Id,
                    Content = [new TextContent { Text = result }]
                });
            }

            messages.Add(new Message { Role = RoleType.User, Content = toolResults });
        }

        _output.WriteLine($"Final response: {finalText}");

        Assert.True(searchInvoked, "LLM did not invoke search");
        Assert.True(readQ3Page, "LLM did not read buildin://q3-page-id");
        Assert.Contains("4.2", finalText);

        await mcpClient.DisposeAsync();
        await server.DisposeAsync();
        c2s.Writer.Complete();
        c2s.Reader.Complete();
        s2c.Writer.Complete();
        s2c.Reader.Complete();
        await sp.DisposeAsync();
    }
}
