using Buildout.Mcp.Auth;
using Microsoft.Extensions.Logging;
using Xunit;
using NSubstitute;

namespace Buildout.UnitTests.Auth;

public sealed class PassthroughAuthenticatorTests
{
    [Fact]
    public async Task AuthenticateAsync_WithValidBearerHeader_ReturnsSuccessWithProvidedToken()
    {
        var logger = Substitute.For<ILogger<PassthroughAuthenticator>>();
        var authenticator = new PassthroughAuthenticator(logger);

        var result = await authenticator.AuthenticateAsync("Bearer client-api-key-123");

        Assert.True(result.IsAuthenticated);
        Assert.Equal("client-api-key-123", result.ResolvedBotToken);
        Assert.Null(result.TokenIdentity);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateAsync_WithBearerHeaderAndSpaces_ReturnsSuccessWithToken()
    {
        var logger = Substitute.For<ILogger<PassthroughAuthenticator>>();
        var authenticator = new PassthroughAuthenticator(logger);

        var result = await authenticator.AuthenticateAsync("Bearer   spaced-token  ");

        Assert.True(result.IsAuthenticated);
        Assert.Equal("spaced-token", result.ResolvedBotToken);
    }

    [Fact]
    public async Task AuthenticateAsync_WithLowercaseBearer_ReturnsSuccessWithToken()
    {
        var logger = Substitute.For<ILogger<PassthroughAuthenticator>>();
        var authenticator = new PassthroughAuthenticator(logger);

        var result = await authenticator.AuthenticateAsync("bearer client-key");

        Assert.True(result.IsAuthenticated);
        Assert.Equal("client-key", result.ResolvedBotToken);
    }

    [Fact]
    public async Task AuthenticateAsync_WithMissingHeader_ReturnsFailure()
    {
        var logger = Substitute.For<ILogger<PassthroughAuthenticator>>();
        var authenticator = new PassthroughAuthenticator(logger);

        var result = await authenticator.AuthenticateAsync(null);

        Assert.False(result.IsAuthenticated);
        Assert.Null(result.ResolvedBotToken);
        Assert.Null(result.TokenIdentity);
        Assert.Equal("Authorization header is required", result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyHeader_ReturnsFailure()
    {
        var logger = Substitute.For<ILogger<PassthroughAuthenticator>>();
        var authenticator = new PassthroughAuthenticator(logger);

        var result = await authenticator.AuthenticateAsync("");

        Assert.False(result.IsAuthenticated);
        Assert.Null(result.ResolvedBotToken);
        Assert.Null(result.TokenIdentity);
        Assert.Equal("Authorization header is required", result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateAsync_WithWhitespaceOnlyHeader_ReturnsFailure()
    {
        var logger = Substitute.For<ILogger<PassthroughAuthenticator>>();
        var authenticator = new PassthroughAuthenticator(logger);

        var result = await authenticator.AuthenticateAsync("   ");

        Assert.False(result.IsAuthenticated);
        Assert.Null(result.ResolvedBotToken);
        Assert.Null(result.TokenIdentity);
        Assert.Equal("Authorization header is required", result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateAsync_WithHeaderWithoutBearer_ReturnsFailure()
    {
        var logger = Substitute.For<ILogger<PassthroughAuthenticator>>();
        var authenticator = new PassthroughAuthenticator(logger);

        var result = await authenticator.AuthenticateAsync("Basic abc123");

        Assert.False(result.IsAuthenticated);
        Assert.Null(result.ResolvedBotToken);
        Assert.Null(result.TokenIdentity);
        Assert.Equal("Authorization header is required", result.ErrorMessage);
    }
}