using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Markdown.Authoring.Properties;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Buildout.UnitTests.Markdown.Authoring;

public class PageCreatorTests
{
    private readonly IBuildinClient _client = Substitute.For<IBuildinClient>();
    private readonly IMarkdownToBlocksParser _parser = Substitute.For<IMarkdownToBlocksParser>();
    private readonly IDatabasePropertyValueParser _propertyParser = Substitute.For<IDatabasePropertyValueParser>();
    private readonly ParentKindProbe _probe;
    private readonly PageCreator _sut;

    private const string ParentPageId = "parent-page-id";

    public PageCreatorTests()
    {
        _probe = new ParentKindProbe(_client);
        _sut = new PageCreator(_probe, _parser, _client, _propertyParser);

        // Default: parent resolves to a page
        _client.GetPageAsync(ParentPageId, Arg.Any<CancellationToken>())
            .Returns(new Page { Id = ParentPageId });

        // Default: parser returns a document with a title and an empty body
        _parser.Parse(Arg.Any<string>())
            .Returns(new AuthoredDocument { Title = "Test Page", Body = [] });
    }

    private static BlockSubtreeWrite MakeBlock(string id = "") =>
        new() { Block = new ParagraphBlock { Id = id }, Children = [] };

    private static BlockSubtreeWrite MakeBlockWithChildren(string id = "") =>
        new()
        {
            Block = new ParagraphBlock { Id = id },
            Children = [new BlockSubtreeWrite { Block = new ParagraphBlock { Id = "" }, Children = [] }]
        };

    [Fact]
    public async Task Icon_ReturnValidation()
    {
        var outcome = await _sut.CreateAsync(new CreatePageInput
        {
            ParentId = ParentPageId,
            Markdown = "# Test",
            Icon = "rocket"
        });

        Assert.Equal(FailureClass.Validation, outcome.FailureClass);
        Assert.Contains("--icon and --cover", outcome.UnderlyingException!.Message);
    }

    [Fact]
    public async Task CoverUrl_ReturnValidation()
    {
        var outcome = await _sut.CreateAsync(new CreatePageInput
        {
            ParentId = ParentPageId,
            Markdown = "# Test",
            CoverUrl = "https://example.com/cover.jpg"
        });

        Assert.Equal(FailureClass.Validation, outcome.FailureClass);
        Assert.Contains("--icon and --cover", outcome.UnderlyingException!.Message);
    }

    [Fact]
    public async Task PropertiesAgainstPageParent_ReturnValidation()
    {
        // Parent resolves to a page (default setup)
        var outcome = await _sut.CreateAsync(new CreatePageInput
        {
            ParentId = ParentPageId,
            Markdown = "# Test",
            Properties = new Dictionary<string, string> { { "Name", "Alice" } }
        });

        Assert.Equal(FailureClass.Validation, outcome.FailureClass);
        Assert.Contains("--property is only valid", outcome.UnderlyingException!.Message);
    }

    [Fact]
    public async Task ProbeAuth401_ReturnsAuth()
    {
        _client.GetPageAsync(ParentPageId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BuildinApiException(new ApiError(401, null, "Unauthorized", null)));
        _client.GetDatabaseAsync(ParentPageId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BuildinApiException(new ApiError(401, null, "Unauthorized", null)));

        var outcome = await _sut.CreateAsync(new CreatePageInput
        {
            ParentId = ParentPageId,
            Markdown = "# Test"
        });

        Assert.Equal(FailureClass.Auth, outcome.FailureClass);
    }

    [Fact]
    public async Task PartialCreation_ReturnsPartialOutcome()
    {
        // Build 101 flat blocks so first batch = 100, remaining = 1 batch
        var body = Enumerable.Range(0, 101).Select(_ => MakeBlock()).ToList();
        _parser.Parse(Arg.Any<string>())
            .Returns(new AuthoredDocument { Title = "Big Page", Body = body });

        _client.CreatePageAsync(Arg.Any<CreatePageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new Page { Id = "page-abc" });

        // First AppendBlockChildrenAsync (for the remaining batch) throws
        _client.AppendBlockChildrenAsync(Arg.Any<string>(), Arg.Any<AppendBlockChildrenRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BuildinApiException(new ApiError(500, null, "Internal Server Error", null)));

        var outcome = await _sut.CreateAsync(new CreatePageInput
        {
            ParentId = ParentPageId,
            Markdown = "# Big Page"
        });

        Assert.Equal(FailureClass.Partial, outcome.FailureClass);
        Assert.Equal("page-abc", outcome.NewPageId);
        Assert.Equal("page-abc", outcome.PartialPageId);
    }
}
