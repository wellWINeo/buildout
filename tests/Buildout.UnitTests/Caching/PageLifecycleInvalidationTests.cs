using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Caching;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

using PageModel = Buildout.Core.Buildin.Models.Page;

namespace Buildout.UnitTests.Caching;

/// <summary>
/// Verifies that PageLifecycle invalidates the cache after a successful delete or restore,
/// and does not invalidate when the page is already in the target state (no-op path).
/// </summary>
[Collection("MetricsTests")]
public sealed class PageLifecycleInvalidationTests
{
    private const string PageId = "test-page-id";

    private readonly IBuildinClient _client = Substitute.For<IBuildinClient>();
    private readonly IPageReadCache _cache = Substitute.For<IPageReadCache>();
    private readonly ILogger<Buildout.Core.PageLifecycle.PageLifecycle> _logger =
        Substitute.For<ILogger<Buildout.Core.PageLifecycle.PageLifecycle>>();
    private readonly Buildout.Core.PageLifecycle.PageLifecycle _sut;

    public PageLifecycleInvalidationTests()
    {
        _sut = new Buildout.Core.PageLifecycle.PageLifecycle(_client, _cache, _logger);
    }

    private static PageModel ActivePage() => new() { Id = PageId, Archived = false };
    private static PageModel ArchivedPage() => new() { Id = PageId, Archived = true };

    [Fact]
    public async Task DeleteAsync_WhenPageIsActive_InvalidatesCache()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>()).Returns(ActivePage());
        _client.UpdatePageAsync(PageId, Arg.Any<UpdatePageRequest>(), Arg.Any<CancellationToken>())
            .Returns(ArchivedPage());

        await _sut.DeleteAsync(PageId);

        _cache.Received(1).Invalidate(PageId);
    }

    [Fact]
    public async Task DeleteAsync_WhenPageAlreadyArchived_DoesNotInvalidateCache()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>()).Returns(ArchivedPage());

        await _sut.DeleteAsync(PageId);

        _cache.DidNotReceive().Invalidate(Arg.Any<string>());
    }

    [Fact]
    public async Task RestoreAsync_WhenPageIsArchived_InvalidatesCache()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>()).Returns(ArchivedPage());
        _client.UpdatePageAsync(PageId, Arg.Any<UpdatePageRequest>(), Arg.Any<CancellationToken>())
            .Returns(ActivePage());

        await _sut.RestoreAsync(PageId);

        _cache.Received(1).Invalidate(PageId);
    }

    [Fact]
    public async Task RestoreAsync_WhenPageAlreadyActive_DoesNotInvalidateCache()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>()).Returns(ActivePage());

        await _sut.RestoreAsync(PageId);

        _cache.DidNotReceive().Invalidate(Arg.Any<string>());
    }
}
