using System;
using System.Collections.Generic;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Caching;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Buildout.UnitTests.Caching;

public sealed class PageReadCacheTests
{
    [Fact]
    public void TryGet_ReturnsFalseForMissingKey()
    {
        var cache = new PageReadCache(new CacheOptions { MaxEntries = 10 });

        var found = cache.TryGet("non-existent", out var entry);

        Assert.False(found);
        Assert.Null(entry);
    }

    [Fact]
    public void StoreThenTryGet_ReturnsEntry()
    {
        var cache = new PageReadCache(new CacheOptions { MaxEntries = 10 });
        var page = new Page { Id = "test-page" };
        var blocks = new List<BlockSubtree>();
        var entry = new PageCacheEntry { Page = page, Blocks = blocks, CachedAt = DateTime.UtcNow };

        cache.Store("test-page", entry);
        var found = cache.TryGet("test-page", out var retrievedEntry);

        Assert.True(found);
        Assert.NotNull(retrievedEntry);
        Assert.Equal("test-page", retrievedEntry.Page.Id);
        Assert.Equal(entry.CachedAt, retrievedEntry.CachedAt);
    }

    [Fact]
    public void Store_ReplacesExistingEntry()
    {
        var cache = new PageReadCache(new CacheOptions { MaxEntries = 10 });
        var page = new Page { Id = "test-page" };
        var blocks = new List<BlockSubtree>();
        var entry1 = new PageCacheEntry { Page = page, Blocks = blocks, CachedAt = DateTime.UtcNow.AddHours(-1) };
        var entry2 = new PageCacheEntry { Page = page, Blocks = blocks, CachedAt = DateTime.UtcNow };

        cache.Store("test-page", entry1);
        cache.Store("test-page", entry2);
        cache.TryGet("test-page", out var retrievedEntry);

        Assert.NotNull(retrievedEntry);
        Assert.Equal(entry2.CachedAt, retrievedEntry.CachedAt);
        Assert.Equal(1, cache.Statistics.Hits);
    }

    [Fact]
    public void Invalidate_RemovesEntry()
    {
        var cache = new PageReadCache(new CacheOptions { MaxEntries = 10 });
        var page = new Page { Id = "test-page" };
        var blocks = new List<BlockSubtree>();
        var entry = new PageCacheEntry { Page = page, Blocks = blocks, CachedAt = DateTime.UtcNow };

        cache.Store("test-page", entry);
        cache.Invalidate("test-page");
        var found = cache.TryGet("test-page", out _);

        Assert.False(found);
    }

    [Fact]
    public void Statistics_TrackHitsAndMisses()
    {
        var cache = new PageReadCache(new CacheOptions { MaxEntries = 10 });
        var page = new Page { Id = "test-page" };
        var blocks = new List<BlockSubtree>();
        var entry = new PageCacheEntry { Page = page, Blocks = blocks, CachedAt = DateTime.UtcNow };

        cache.Store("test-page", entry);

        cache.TryGet("test-page", out _);
        cache.TryGet("test-page", out _);
        cache.TryGet("non-existent", out _);

        Assert.Equal(2, cache.Statistics.Hits);
        Assert.Equal(1, cache.Statistics.Misses);
        Assert.Equal(0, cache.Statistics.Evictions);
    }

    [Fact]
    public void EvictAtCapacity_EvictsLRUEntry()
    {
        var cache = new PageReadCache(new CacheOptions { MaxEntries = 3 });

        cache.Store("page-1", CreateEntry());
        cache.Store("page-2", CreateEntry());
        cache.Store("page-3", CreateEntry());

        Assert.True(cache.TryGet("page-1", out _));
        Assert.True(cache.TryGet("page-2", out _));
        Assert.True(cache.TryGet("page-3", out _));

        cache.Store("page-4", CreateEntry());

        Assert.False(cache.TryGet("page-1", out _));
        Assert.True(cache.TryGet("page-2", out _));
        Assert.True(cache.TryGet("page-3", out _));
        Assert.True(cache.TryGet("page-4", out _));
        Assert.Equal(1, cache.Statistics.Evictions);
    }

    [Fact]
    public void ReaccessedEntry_MovesToFront_NotEvicted()
    {
        var cache = new PageReadCache(new CacheOptions { MaxEntries = 3 });

        cache.Store("page-1", CreateEntry());
        cache.Store("page-2", CreateEntry());
        cache.Store("page-3", CreateEntry());

        cache.TryGet("page-1", out _);

        cache.Store("page-4", CreateEntry());

        Assert.True(cache.TryGet("page-1", out _));
        Assert.False(cache.TryGet("page-2", out _));
        Assert.True(cache.TryGet("page-3", out _));
        Assert.True(cache.TryGet("page-4", out _));
        Assert.Equal(1, cache.Statistics.Evictions);
    }

    [Fact]
    public void Statistics_TrackEvictions()
    {
        var cache = new PageReadCache(new CacheOptions { MaxEntries = 2 });

        cache.Store("page-1", CreateEntry());
        cache.Store("page-2", CreateEntry());
        cache.Store("page-3", CreateEntry());

        Assert.Equal(1, cache.Statistics.Evictions);

        cache.Store("page-4", CreateEntry());

        Assert.Equal(2, cache.Statistics.Evictions);
    }

    private static PageCacheEntry CreateEntry() => new()
    {
        Page = new Page { Id = Guid.NewGuid().ToString() },
        Blocks = new List<BlockSubtree>(),
        CachedAt = DateTime.UtcNow
    };
}