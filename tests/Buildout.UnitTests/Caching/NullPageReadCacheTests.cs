using System;
using System.Collections.Generic;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Caching;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Internal;
using Xunit;

namespace Buildout.UnitTests.Caching;

public sealed class NullPageReadCacheTests
{
    [Fact]
    public void TryGet_AlwaysReturnsFalse()
    {
        var cache = new NullPageReadCache();

        var found = cache.TryGet("any-key", out var entry);

        Assert.False(found);
        Assert.Null(entry);
    }

    [Fact]
    public void TryGet_MultipleCalls_AllReturnFalse()
    {
        var cache = new NullPageReadCache();

        Assert.False(cache.TryGet("key-1", out _));
        Assert.False(cache.TryGet("key-2", out _));
        Assert.False(cache.TryGet("key-3", out _));
    }

    [Fact]
    public void Store_IsNoOp()
    {
        var cache = new NullPageReadCache();
        var page = new Page { Id = "test-page" };
        var blocks = new List<BlockSubtree>();
        var entry = new PageCacheEntry { Page = page, Blocks = blocks, CachedAt = DateTime.UtcNow };

        cache.Store("test-page", entry);

        Assert.False(cache.TryGet("test-page", out _));
    }

    [Fact]
    public void Invalidate_IsNoOp()
    {
        var cache = new NullPageReadCache();

        cache.Invalidate("any-key");

        Assert.True(true);
    }

    [Fact]
    public void Invalidate_MultipleCalls_NoEffect()
    {
        var cache = new NullPageReadCache();

        cache.Invalidate("key-1");
        cache.Invalidate("key-2");
        cache.Invalidate("key-3");

        Assert.True(true);
    }

    [Fact]
    public void Statistics_AllZeros()
    {
        var cache = new NullPageReadCache();

        var stats = cache.Statistics;

        Assert.Equal(0, stats.Hits);
        Assert.Equal(0, stats.Misses);
        Assert.Equal(0, stats.Evictions);
    }

    [Fact]
    public void Statistics_AfterOperations_RemainZero()
    {
        var cache = new NullPageReadCache();
        var entry = new PageCacheEntry
        {
            Page = new Page { Id = "test" },
            Blocks = new List<BlockSubtree>(),
            CachedAt = DateTime.UtcNow
        };

        cache.TryGet("key", out _);
        cache.Store("key", entry);
        cache.Invalidate("key");
        cache.TryGet("key", out _);

        Assert.Equal(0, cache.Statistics.Hits);
        Assert.Equal(0, cache.Statistics.Misses);
        Assert.Equal(0, cache.Statistics.Evictions);
    }
}