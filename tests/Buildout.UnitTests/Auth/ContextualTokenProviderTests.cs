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
}