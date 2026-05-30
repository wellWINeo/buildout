using Buildout.Mcp.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using NSubstitute;

namespace Buildout.UnitTests.Auth;

public sealed class ProxyAuthenticatorTests
{
    [Fact]
    public async Task AuthenticateAsync_WithValidToken_ReturnsSuccessWithGlobalToken()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotToken"] = "global-bot-token"
            })
            .Build();

        var tokenStore = Substitute.For<ITokenStore>();
        tokenStore.ValidateTokenAsync(Arg.Any<string>())
            .Returns(new McpTokenRecord(Guid.NewGuid(), "test-token", "hashed-token", null, DateTimeOffset.UtcNow, null));

        var logger = Substitute.For<ILogger<ProxyAuthenticator>>();
        var authenticator = new ProxyAuthenticator(configuration, tokenStore, logger);

        var result = await authenticator.AuthenticateAsync("Bearer valid-token");

        Assert.True(result.IsAuthenticated);
        Assert.Equal("global-bot-token", result.ResolvedBotToken);
        Assert.Equal("test-token", result.TokenIdentity);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateAsync_WithMissingHeader_ReturnsFailure()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotToken"] = "global-bot-token"
            })
            .Build();

        var tokenStore = Substitute.For<ITokenStore>();
        var logger = Substitute.For<ILogger<ProxyAuthenticator>>();
        var authenticator = new ProxyAuthenticator(configuration, tokenStore, logger);

        var result = await authenticator.AuthenticateAsync(null);

        Assert.False(result.IsAuthenticated);
        Assert.Null(result.ResolvedBotToken);
        Assert.Null(result.TokenIdentity);
        Assert.Equal("Authorization header is required", result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidToken_ReturnsFailure()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotToken"] = "global-bot-token"
            })
            .Build();

        var tokenStore = Substitute.For<ITokenStore>();
        tokenStore.ValidateTokenAsync(Arg.Any<string>())
            .Returns((McpTokenRecord?)null);

        var logger = Substitute.For<ILogger<ProxyAuthenticator>>();
        var authenticator = new ProxyAuthenticator(configuration, tokenStore, logger);

        var result = await authenticator.AuthenticateAsync("Bearer invalid-token");

        Assert.False(result.IsAuthenticated);
        Assert.Null(result.ResolvedBotToken);
        Assert.Null(result.TokenIdentity);
        Assert.Equal("Invalid or revoked token", result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateAsync_WithRevokedToken_ReturnsFailure()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotToken"] = "global-bot-token"
            })
            .Build();

        var tokenStore = Substitute.For<ITokenStore>();
        tokenStore.ValidateTokenAsync(Arg.Any<string>())
            .Returns(new McpTokenRecord(Guid.NewGuid(), "revoked-token", "hashed-token", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var logger = Substitute.For<ILogger<ProxyAuthenticator>>();
        var authenticator = new ProxyAuthenticator(configuration, tokenStore, logger);

        var result = await authenticator.AuthenticateAsync("Bearer revoked-token");

        Assert.False(result.IsAuthenticated);
        Assert.Null(result.ResolvedBotToken);
        Assert.Null(result.TokenIdentity);
        Assert.Equal("Invalid or revoked token", result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyHeader_ReturnsFailure()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotToken"] = "global-bot-token"
            })
            .Build();

        var tokenStore = Substitute.For<ITokenStore>();
        var logger = Substitute.For<ILogger<ProxyAuthenticator>>();
        var authenticator = new ProxyAuthenticator(configuration, tokenStore, logger);

        var result = await authenticator.AuthenticateAsync("");

        Assert.False(result.IsAuthenticated);
        Assert.Null(result.ResolvedBotToken);
        Assert.Null(result.TokenIdentity);
        Assert.Equal("Authorization header is required", result.ErrorMessage);
    }
}