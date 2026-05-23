using System.Reflection;
using Buildout.Configuration;
using Buildout.Core.Buildin;
using Buildout.Core.Diagnostics;
using Buildout.Core.Markdown.Editing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Buildout.IntegrationTests.Configuration;

public sealed class PrecedenceMatrixTests
{
    [Fact]
    public void DefaultUsed_WhenNothingSet()
    {
        Environment.SetEnvironmentVariable("Buildout__BotToken", "env-token");
        Environment.SetEnvironmentVariable("Buildout__Telemetry__OtlpEndpoint", null);

        try
        {
            var config = BuildoutConfiguration.Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddOptions<BuildinClientOptions>()
                .Bind(config)
                .ValidateOnStart();
            services.AddOptions<TelemetryOptions>()
                .Bind(config.GetSection("Telemetry"))
                .ValidateOnStart();
            services.AddOptions<LimitationsOptions>()
                .Bind(config.GetSection("Limitations"))
                .ValidateOnStart();

            var provider = services.BuildServiceProvider();

            var telemetryOptions = provider.GetRequiredService<IOptions<TelemetryOptions>>().Value;

            Assert.Equal(new Uri("http://localhost:4318"), telemetryOptions.OtlpEndpoint);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Buildout__BotToken", null);
        }
    }

    [Fact]
    public void FileOverridesDefault()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"buildout-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var configPath = Path.Combine(tempDir, "config.json");
        var jsonConfig = """
            {
                "BotToken": "file-token",
                "Telemetry": {
                    "OtlpEndpoint": "http://custom-otel:4318"
                }
            }
            """;

        try
        {
            File.WriteAllText(configPath, jsonConfig);

            var config = BuildoutConfiguration.Build(configPath);

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddOptions<BuildinClientOptions>()
                .Bind(config)
                .ValidateOnStart();
            services.AddOptions<TelemetryOptions>()
                .Bind(config.GetSection("Telemetry"))
                .ValidateOnStart();

            var provider = services.BuildServiceProvider();

            var telemetryOptions = provider.GetRequiredService<IOptions<TelemetryOptions>>().Value;

            Assert.Equal(new Uri("http://custom-otel:4318"), telemetryOptions.OtlpEndpoint);
        }
        finally
        {
            if (File.Exists(configPath)) File.Delete(configPath);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void BuildoutEnvVar_OverridesFileForThatKeyOnly_OtherKeysFromFileUnchanged()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"buildout-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var configPath = Path.Combine(tempDir, "config.json");
        var jsonConfig = """
            {
                "BotToken": "file-token",
                "BaseUrl": "https://file.example.com/",
                "Http": {
                    "Timeout": "00:00:30",
                    "UnsafeAllowInsecure": false
                }
            }
            """;

        Environment.SetEnvironmentVariable("Buildout__Http__Timeout", "00:01:00");

        try
        {
            File.WriteAllText(configPath, jsonConfig);

            var config = BuildoutConfiguration.Build(configPath);

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddOptions<BuildinClientOptions>()
                .Bind(config)
                .ValidateOnStart();

            var provider = services.BuildServiceProvider();

            var buildinOptions = provider.GetRequiredService<IOptions<BuildinClientOptions>>().Value;

            Assert.Equal(TimeSpan.FromMinutes(1), buildinOptions.Http.Timeout);
            Assert.Equal(new Uri("https://file.example.com/"), buildinOptions.BaseUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Buildout__Http__Timeout", null);
            if (File.Exists(configPath)) File.Delete(configPath);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void BuildoutHttpTimeoutAlone_DoesNotBlankHttpUnsafeAllowInsecure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"buildout-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var configPath = Path.Combine(tempDir, "config.json");
        var jsonConfig = """
            {
                "BotToken": "file-token",
                "Http": {
                    "Timeout": "00:00:30",
                    "UnsafeAllowInsecure": true
                }
            }
            """;

        Environment.SetEnvironmentVariable("Buildout__Http__Timeout", "00:01:00");

        try
        {
            File.WriteAllText(configPath, jsonConfig);

            var config = BuildoutConfiguration.Build(configPath);

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddOptions<BuildinClientOptions>()
                .Bind(config)
                .ValidateOnStart();

            var provider = services.BuildServiceProvider();

            var buildinOptions = provider.GetRequiredService<IOptions<BuildinClientOptions>>().Value;

            Assert.Equal(TimeSpan.FromMinutes(1), buildinOptions.Http.Timeout);
            Assert.True(buildinOptions.Http.UnsafeAllowInsecure);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Buildout__Http__Timeout", null);
            if (File.Exists(configPath)) File.Delete(configPath);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}