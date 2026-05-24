using Buildout.Core.Buildin.Models;
using Buildout.Core.Caching;
using Buildout.Core.Markdown.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Buildout.UnitTests.Caching;

public sealed class CachingPageContentProviderTests
{
    private const string PageId1 = "page1";
    private const string PageId2 = "page2";

    [Fact]
    public async Task FetchAsync_CacheMiss_FetchesViaDelegate()
    {
        var cache = new PageReadCache(new CacheOptions { MaxEntries = 10 });
        var callCount = 0;
        PageContent CreateContent(string pageId) => new()
        {
            Page = new Page { Id = pageId, Title = [new RichText { Type = "text", Content = "Test" }] },
            Blocks = []
        };

        var provider = new CachingPageContentProvider(
            cache,
            async (pageId, ct) =>
            {
                callCount++;
                return CreateContent(pageId);
            });

        var result = await provider.FetchAsync(PageId1, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(PageId1, result.Page.Id);
        Assert.Equal(1, callCount);
        Assert.Equal(1, cache.Statistics.Misses);
    }

    [Fact]
    public async Task FetchAsync_SecondCall_ReturnsCachedResultWithoutDelegateInvocation()
    {
        var cache = new PageReadCache(new CacheOptions { MaxEntries = 10 });
        var callCount = 0;
        PageContent CreateContent(string pageId) => new()
        {
            Page = new Page { Id = pageId, Title = [new RichText { Type = "text", Content = "Test" }] },
            Blocks = []
        };

        var provider = new CachingPageContentProvider(
            cache,
            async (pageId, ct) =>
            {
                callCount++;
                return CreateContent(pageId);
            });

        var result1 = await provider.FetchAsync(PageId1, CancellationToken.None);
        var result2 = await provider.FetchAsync(PageId1, CancellationToken.None);

        Assert.Equal(result1.Page.Id, result2.Page.Id);
        Assert.Equal(1, callCount);
        Assert.Equal(1, cache.Statistics.Hits);
        Assert.Equal(1, cache.Statistics.Misses);
    }

    [Fact]
    public async Task FetchAsync_ErrorResultIsNotCached()
    {
        var cache = new PageReadCache(new CacheOptions { MaxEntries = 10 });
        var callCount = 0;

        var provider = new CachingPageContentProvider(
            cache,
            async (pageId, ct) =>
            {
                callCount++;
                throw new InvalidOperationException("Test error");
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.FetchAsync(PageId1, CancellationToken.None));

        var found = cache.TryGet(PageId1, out _);
        Assert.False(found);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task FetchAsync_ErrorThenSuccess_CachesSuccessfulResult()
    {
        var cache = new PageReadCache(new CacheOptions { MaxEntries = 10 });
        var callCount = 0;
        var shouldFail = true;

        var provider = new CachingPageContentProvider(
            cache,
            async (pageId, ct) =>
            {
                callCount++;
                if (shouldFail)
                {
                    throw new InvalidOperationException("Test error");
                }

                shouldFail = false;
                return new PageContent
                {
                    Page = new Page { Id = pageId, Title = [new RichText { Type = "text", Content = "Test" }] },
                    Blocks = []
                };
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.FetchAsync(PageId1, CancellationToken.None));

        var result = await provider.FetchAsync(PageId1, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, callCount);
        Assert.Equal(1, cache.Statistics.Hits);
    }

    [Fact]
    public async Task FetchAsync_ConcurrentReads_SingleFetch()
    {
        var cache = new PageReadCache(new CacheOptions { MaxEntries = 10 });
        var callCount = 0;
        var semaphore = new SemaphoreSlim(0, 1);
        var readyToFetch = new SemaphoreSlim(0, 1);
        PageContent CreateContent(string pageId) => new()
        {
            Page = new Page { Id = pageId, Title = [new RichText { Type = "text", Content = "Test" }] },
            Blocks = []
        };

        var provider = new CachingPageContentProvider(
            cache,
            async (pageId, ct) =>
            {
                readyToFetch.Release(2);
                await semaphore.WaitAsync(ct);
                callCount++;
                return CreateContent(pageId);
            });

        var task1 = provider.FetchAsync(PageId1, CancellationToken.None);
        var task2 = provider.FetchAsync(PageId1, CancellationToken.None);

        readyToFetch.Wait();
        readyToFetch.Wait();

        await Task.Delay(50);
        semaphore.Release(2);

        await Task.WhenAll(task1, task2);

        Assert.Equal(1, callCount);
        Assert.Equal(1, cache.Statistics.Misses);
    }

    [Fact]
    public async Task FetchAsync_EmptyBlockTree_CachesAsValidEntry()
    {
        var cache = new PageReadCache(new CacheOptions { MaxEntries = 10 });
        PageContent CreateContent(string pageId) => new()
        {
            Page = new Page { Id = pageId, Title = [new RichText { Type = "text", Content = "Empty" }] },
            Blocks = []
        };

        var provider = new CachingPageContentProvider(
            cache,
            async (pageId, ct) => CreateContent(pageId));

        var result1 = await provider.FetchAsync(PageId1, CancellationToken.None);
        var result2 = await provider.FetchAsync(PageId1, CancellationToken.None);

        Assert.NotNull(result1);
        Assert.Empty(result1.Blocks);
        Assert.Equal(result1.Page.Id, result2.Page.Id);
        Assert.Equal(1, cache.Statistics.Hits);
        Assert.Equal(1, cache.Statistics.Misses);
    }

    [Fact]
    public async Task FetchAsync_ExceptionNotCached_NextCallRetries()
    {
        var cache = new PageReadCache(new CacheOptions { MaxEntries = 10 });
        var callCount = 0;
        var shouldFail = true;

        var provider = new CachingPageContentProvider(
            cache,
            async (pageId, ct) =>
            {
                callCount++;
                if (shouldFail)
                {
                    throw new HttpRequestException("Network error");
                }

                return new PageContent
                {
                    Page = new Page { Id = pageId, Title = [new RichText { Type = "text", Content = "Success" }] },
                    Blocks = []
                };
            });

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.FetchAsync(PageId1, CancellationToken.None));

        var found = cache.TryGet(PageId1, out _);
        Assert.False(found);

        shouldFail = false;
        var result = await provider.FetchAsync(PageId1, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, callCount);
        Assert.Equal(0, cache.Statistics.Hits);
        Assert.Equal(2, cache.Statistics.Misses);
    }
}