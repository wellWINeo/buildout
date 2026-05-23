using Buildout.Core.Buildin;
using Buildout.Configuration;
using Microsoft.Extensions.Configuration;
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
            Http = new HttpOptions { Timeout = TimeSpan.FromSeconds(30) }
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
            Http = new HttpOptions { Timeout = TimeSpan.FromSeconds(30) }
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
            Http = new HttpOptions { Timeout = TimeSpan.FromSeconds(30) }
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
            Http = new HttpOptions { Timeout = TimeSpan.FromSeconds(30), UnsafeAllowInsecure = false }
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
            Http = new HttpOptions { Timeout = TimeSpan.FromSeconds(30), UnsafeAllowInsecure = true }
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
            Http = new HttpOptions { Timeout = TimeSpan.FromSeconds(30) }
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
            Http = new HttpOptions { Timeout = TimeSpan.FromSeconds(30) }
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
            Http = new HttpOptions { Timeout = TimeSpan.Zero }
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Http:Timeout", result.FailureMessage);
    }

    [Fact]
    public void NegativeTimeout_FailsValidation()
    {
        var options = new BuildinClientOptions
        {
            BaseUrl = new Uri("https://api.buildin.ai/"),
            BotToken = "valid-token",
            Http = new HttpOptions { Timeout = TimeSpan.FromSeconds(-5) }
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Http:Timeout", result.FailureMessage);
    }

    public sealed class BuildoutConfigurationBindingTests
    {
        [Fact]
        public void BuildoutConfiguration_Build_BindsBotTokenFromJson()
        {
            var jsonConfig = """
                {
                    "BotToken": "test-token-123",
                    "BaseUrl": "https://custom.api.com/"
                }
                """;

            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, jsonConfig);

                var (config, _) = BuildoutConfiguration.Build(["--config", tempFile]);

                var options = new BuildinClientOptions();
                config.Bind(options);

                Assert.Equal("test-token-123", options.BotToken);
                Assert.Equal(new Uri("https://custom.api.com/"), options.BaseUrl);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void BuildoutConfiguration_Build_BindsAllPropertiesFromJson()
        {
            var jsonConfig = """
                {
                    "BotToken": "test-token-456",
                    "BaseUrl": "https://custom.api.com/",
                    "Http": {
                        "Timeout": "00:02:00",
                        "UnsafeAllowInsecure": true
                    }
                }
                """;

            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, jsonConfig);

                var (config, _) = BuildoutConfiguration.Build(["--config", tempFile]);

                var options = new BuildinClientOptions();
                config.Bind(options);

                Assert.Equal("test-token-456", options.BotToken);
                Assert.Equal(new Uri("https://custom.api.com/"), options.BaseUrl);
                Assert.Equal(TimeSpan.FromMinutes(2), options.Http.Timeout);
                Assert.True(options.Http.UnsafeAllowInsecure);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void BuildoutConfiguration_Build_EnvVarOverridesJson()
        {
            var jsonConfig = """
                {
                    "BotToken": "json-token",
                    "BaseUrl": "https://json.api.com/"
                }
                """;

            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, jsonConfig);
                Environment.SetEnvironmentVariable("Buildout__BotToken", "env-token-789");

                var (config, _) = BuildoutConfiguration.Build(["--config", tempFile]);

                var options = new BuildinClientOptions();
                config.Bind(options);

                Assert.Equal("env-token-789", options.BotToken);
                Assert.Equal(new Uri("https://json.api.com/"), options.BaseUrl);
            }
            finally
            {
                File.Delete(tempFile);
                Environment.SetEnvironmentVariable("Buildout__BotToken", null);
            }
        }

        [Fact]
        public void BuildoutConfiguration_Build_ResidualArgsPreserved()
        {
            var jsonConfig = """
                {
                    "BotToken": "test-token"
                }
                """;

            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, jsonConfig);

                var args = new[] { "--config", tempFile, "create", "--title", "Test Page" };
                var (_, residualArgs) = BuildoutConfiguration.Build(args);

                Assert.Equal(new[] { "create", "--title", "Test Page" }, residualArgs);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
