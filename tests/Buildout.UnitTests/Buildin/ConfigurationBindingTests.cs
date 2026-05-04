using Buildout.Core.Buildin;
using Microsoft.Extensions.Options;
using Xunit;

namespace Buildout.UnitTests.Buildin;

public sealed class ConfigurationBindingTests
{
    private readonly BuildinClientOptionsValidator _validator = new();

    [Fact]
    public void ValidOptions_PassValidation()
    {
        var options = new BuildinClientOptions
        {
            BaseUrl = new Uri("https://api.buildin.ai/"),
            BotToken = "valid-token",
            HttpTimeout = TimeSpan.FromSeconds(30)
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void NullBaseUrl_FailsValidation()
    {
        var options = new BuildinClientOptions
        {
            BaseUrl = null!,
            BotToken = "valid-token",
            HttpTimeout = TimeSpan.FromSeconds(30)
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("BaseUrl", result.FailureMessage);
    }

    [Fact]
    public void RelativeBaseUrl_FailsValidation()
    {
        var options = new BuildinClientOptions
        {
            BaseUrl = new Uri("/relative", UriKind.Relative),
            BotToken = "valid-token",
            HttpTimeout = TimeSpan.FromSeconds(30)
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("absolute", result.FailureMessage);
    }

    [Fact]
    public void HttpBaseUrl_FailsValidation_WhenNotInsecure()
    {
        var options = new BuildinClientOptions
        {
            BaseUrl = new Uri("http://api.buildin.ai/"),
            BotToken = "valid-token",
            HttpTimeout = TimeSpan.FromSeconds(30),
            UnsafeAllowInsecure = false
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("HTTPS", result.FailureMessage);
    }

    [Fact]
    public void HttpBaseUrl_PassesValidation_WhenInsecureAllowed()
    {
        var options = new BuildinClientOptions
        {
            BaseUrl = new Uri("http://localhost:8080/"),
            BotToken = "valid-token",
            HttpTimeout = TimeSpan.FromSeconds(30),
            UnsafeAllowInsecure = true
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void EmptyBotToken_FailsValidation()
    {
        var options = new BuildinClientOptions
        {
            BaseUrl = new Uri("https://api.buildin.ai/"),
            BotToken = "",
            HttpTimeout = TimeSpan.FromSeconds(30)
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("BotToken", result.FailureMessage);
    }

    [Fact]
    public void WhitespaceBotToken_FailsValidation()
    {
        var options = new BuildinClientOptions
        {
            BaseUrl = new Uri("https://api.buildin.ai/"),
            BotToken = "   ",
            HttpTimeout = TimeSpan.FromSeconds(30)
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("BotToken", result.FailureMessage);
    }

    [Fact]
    public void ZeroTimeout_FailsValidation()
    {
        var options = new BuildinClientOptions
        {
            BaseUrl = new Uri("https://api.buildin.ai/"),
            BotToken = "valid-token",
            HttpTimeout = TimeSpan.Zero
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("HttpTimeout", result.FailureMessage);
    }

    [Fact]
    public void NegativeTimeout_FailsValidation()
    {
        var options = new BuildinClientOptions
        {
            BaseUrl = new Uri("https://api.buildin.ai/"),
            BotToken = "valid-token",
            HttpTimeout = TimeSpan.FromSeconds(-5)
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("HttpTimeout", result.FailureMessage);
    }
}
