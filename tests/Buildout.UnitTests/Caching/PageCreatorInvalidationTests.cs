using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Caching;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Markdown.Authoring.Properties;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Buildout.UnitTests.Caching;

/// <summary>
/// Verifies that PageCreator.CreateAsync invalidates the parent page's cache entry
/// after a successful create, and does not invalidate on failure.
/// </summary>
public sealed class PageCreatorInvalidationTests
{
    private const string ParentPageId = "parent-page-id";
    private const string NewPageId = "new-page-id";

    private readonly IBuildinClient _client = Substitute.For<IBuildinClient>();
    private readonly IMarkdownToBlocksParser _parser = Substitute.For<IMarkdownToBlocksParser>();
    private readonly IDatabasePropertyValueParser _propertyParser = Substitute.For<IDatabasePropertyValueParser>();
    private readonly ILogger<PageCreator> _logger = Substitute.For<ILogger<PageCreator>>();
    private readonly IPageReadCache _cache = Substitute.For<IPageReadCache>();
    private readonly PageCreator _sut;

    public PageCreatorInvalidationTests()
    {
        var probe = new ParentKindProbe(_client);
        _sut = new PageCreator(probe, _parser, _client, _propertyParser, _cache, _logger);

        // Default: parent resolves to a page
        _client.GetPageAsync(ParentPageId, Arg.Any<CancellationToken>())
            .Returns(new Page { Id = ParentPageId });

        // Default: parser returns a document with a title and empty body
        _parser.Parse(Arg.Any<string>())
            .Returns(new AuthoredDocument { Title = "Test Page", Body = [] });
    }

    [Fact]
    public async Task CreateAsync_OnSuccess_InvalidatesParentCache()
    {
        _client.CreatePageAsync(Arg.Any<CreatePageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new Page { Id = NewPageId });

        var outcome = await _sut.CreateAsync(new CreatePageInput
        {
            ParentId = ParentPageId,
            Markdown = "# Test Page"
        });

        Assert.Equal(NewPageId, outcome.NewPageId);
        _cache.Received(1).Invalidate(ParentPageId);
    }

    [Fact]
    public async Task CreateAsync_WhenApiCallFails_DoesNotInvalidateCache()
    {
        _client.CreatePageAsync(Arg.Any<CreatePageRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BuildinApiException(new ApiError(400, null, "Bad Request", null)));

        await _sut.CreateAsync(new CreatePageInput
        {
            ParentId = ParentPageId,
            Markdown = "# Test Page"
        });

        _cache.DidNotReceive().Invalidate(Arg.Any<string>());
    }
}
