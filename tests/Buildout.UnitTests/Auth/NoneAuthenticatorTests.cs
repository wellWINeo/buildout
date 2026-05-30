using Xunit;
using Buildout.Core.Auth;
using Buildout.Mcp.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Buildout.UnitTests.Auth;

public sealed class NoneAuthenticatorTests
{
    [Fact]
    public async Task AuthenticateAsync_NoHeader_ReturnsSuccessWithGlobalToken()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotToken"] = "test-bot-token"
            })
            .Build();

        var logger = NSubstitute.Substitute.For<ILogger<NoneAuthenticator>>();
        var authenticator = new NoneAuthenticator(configuration, logger);

        var result = await authenticator.AuthenticateAsync(null);

        Assert.True(result.IsAuthenticated);
        Assert.Equal("test-bot-token", result.ResolvedBotToken);
        Assert.Null(result.TokenIdentity);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateAsync_WithHeader_IgnoresHeaderReturnsGlobalToken()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotToken"] = "test-bot-token"
            })
            .Build();

        var logger = NSubstitute.Substitute.For<ILogger<NoneAuthenticator>>();
        var authenticator = new NoneAuthenticator(configuration, logger);

        var result = await authenticator.AuthenticateAsync("Bearer some-client-token");

        Assert.True(result.IsAuthenticated);
        Assert.Equal("test-bot-token", result.ResolvedBotToken);
        Assert.Null(result.TokenIdentity);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyHeader_ReturnsSuccessWithGlobalToken()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotToken"] = "test-bot-token"
            })
            .Build();

        var logger = NSubstitute.Substitute.For<ILogger<NoneAuthenticator>>();
        var authenticator = new NoneAuthenticator(configuration, logger);

        var result = await authenticator.AuthenticateAsync("");

        Assert.True(result.IsAuthenticated);
        Assert.Equal("test-bot-token", result.ResolvedBotToken);
        Assert.Null(result.TokenIdentity);
        Assert.Null(result.ErrorMessage);
    }
}