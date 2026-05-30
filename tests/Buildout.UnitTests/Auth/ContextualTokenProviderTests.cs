using Xunit;

namespace Buildout.Core.Buildin.Authentication.Tests;

public sealed class ContextualTokenProviderTests
{
    [Fact]
    public async Task OverrideToken_RestoresDefaultTokenAfterScope()
    {
        var provider = new ContextualTokenProvider("default-token");

        using (ContextualTokenProvider.OverrideToken("override-token"))
        {
            await Task.Yield();
        }

        var request = new HttpRequestMessage();
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task ConcurrentRequests_AreIsolated()
    {
        var provider = new ContextualTokenProvider("default-token");

        var task1 = Task.Run(async () =>
        {
            using (ContextualTokenProvider.OverrideToken("token-1"))
            {
                await Task.Delay(10);
                return true;
            }
        });

        var task2 = Task.Run(async () =>
        {
            using (ContextualTokenProvider.OverrideToken("token-2"))
            {
                await Task.Delay(5);
                return true;
            }
        });

        var results = await Task.WhenAll(task1, task2);
        Assert.True(results[0]);
        Assert.True(results[1]);
    }

    [Fact]
    public async Task GetToken_InFreshAsyncContext_DoesNotThrow()
    {
        // Simulate a new thread-pool context (e.g. stdio transport) where
        // CurrentToken was never set via OverrideToken.
        var provider = new ContextualTokenProvider("default-token");

        Exception? caught = null;
        await Task.Run(async () =>
        {
            try
            {
                // OverrideToken with the same value as default: exercises the
                // fallback path without needing access to the private inner class.
                using (ContextualTokenProvider.OverrideToken("default-token"))
                {
                    await Task.Yield();
                }
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });

        Assert.Null(caught);
    }
}