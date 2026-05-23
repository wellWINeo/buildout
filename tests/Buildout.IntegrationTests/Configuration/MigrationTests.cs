using System.Text;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Buildout.IntegrationTests.Configuration;

public sealed class MigrationTests
{
    [Fact]
    public void LegacyKey_BuildinBotToken_ProducesWarningContainingBotToken()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"buildout-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var configPath = Path.Combine(tempDir, "config.json");
        var jsonConfig = """
            {
                "Buildin:BotToken": "legacy-token-123",
                "BotToken": "test-token-123"
            }
            """;

        try
        {
            File.WriteAllText(configPath, jsonConfig);

            var args = new[] { "--config", configPath };

            using var logCapture = new LogCapture();
            var options = new Buildout.Core.Configuration.BuildoutConfigurationOptions();
            var (config, _) = Buildout.Core.Configuration.BuildoutConfiguration.Build(args, options, logCapture.Logger);

            var warnings = logCapture.GetWarnings();
            var botTokenWarning = warnings.FirstOrDefault(w => w.Contains("Buildin:BotToken"));

            Assert.NotNull(botTokenWarning);
            Assert.Contains("BotToken", botTokenWarning);
            Assert.Contains("Buildin:BotToken", botTokenWarning);
        }
        finally
        {
            if (File.Exists(configPath))
                File.Delete(configPath);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LegacyKey_PageEditorLargeDeleteThreshold_WarningNamesLimitationsLargeDeleteThreshold()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"buildout-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var configPath = Path.Combine(tempDir, "config.json");
        var jsonConfig = """
            {
                "PageEditor:LargeDeleteThreshold": 50,
                "BotToken": "test-token-123"
            }
            """;

        try
        {
            File.WriteAllText(configPath, jsonConfig);

            var args = new[] { "--config", configPath };

            using var logCapture = new LogCapture();
            var options = new Buildout.Core.Configuration.BuildoutConfigurationOptions();
            var (config, _) = Buildout.Core.Configuration.BuildoutConfiguration.Build(args, options, logCapture.Logger);

            var warnings = logCapture.GetWarnings();
            var legacyWarning = warnings.FirstOrDefault(w => w.Contains("PageEditor:LargeDeleteThreshold"));

            Assert.NotNull(legacyWarning);
            Assert.Contains("Limitations:LargeDeleteThreshold", legacyWarning);
            Assert.Contains("Buildout__Limitations__LargeDeleteThreshold", legacyWarning);
        }
        finally
        {
            if (File.Exists(configPath))
                File.Delete(configPath);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LegacyEnvVar_BuildoutTelemetryEnabled_WarningNamesBuildoutTelemetryEnabled()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"buildout-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        Environment.SetEnvironmentVariable("BUILDOUT_TELEMETRY_ENABLED", "true");

        try
        {
            var args = Array.Empty<string>();

            using var logCapture = new LogCapture();
            var options = new Buildout.Core.Configuration.BuildoutConfigurationOptions
            {
                DefaultFilePath = Path.Combine(tempDir, "config.json")
            };
            File.WriteAllText(options.DefaultFilePath, """{"BotToken":"test-token-123"}""");
            var (config, _) = Buildout.Core.Configuration.BuildoutConfiguration.Build(args, options, logCapture.Logger);

            var warnings = logCapture.GetWarnings();
            var legacyWarning = warnings.FirstOrDefault(w => w.Contains("BUILDOUT_TELEMETRY_ENABLED"));

            Assert.NotNull(legacyWarning);
            Assert.Contains("Telemetry:Enabled", legacyWarning);
            Assert.Contains("Buildout__Telemetry__Enabled", legacyWarning);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUILDOUT_TELEMETRY_ENABLED", null);
        }
    }

    [Fact]
    public void LegacyEnvVar_OtelExporterOtlpEndpoint_ProducesNoWarning_HonouredAsFallback()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"buildout-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://otel-collector:4318");

        try
        {
            var args = Array.Empty<string>();

            using var logCapture = new LogCapture();
            var options = new Buildout.Core.Configuration.BuildoutConfigurationOptions
            {
                DefaultFilePath = Path.Combine(tempDir, "config.json")
            };
            File.WriteAllText(options.DefaultFilePath, """{"BotToken":"test-token-123"}""");
            var (config, _) = Buildout.Core.Configuration.BuildoutConfiguration.Build(args, options, logCapture.Logger);

            var warnings = logCapture.GetWarnings();
            var otlpWarning = warnings.FirstOrDefault(w => w.Contains("OTEL_EXPORTER_OTLP_ENDPOINT"));

            Assert.Null(otlpWarning);

            var otlpEndpoint = config["Telemetry:OtlpEndpoint"];
            Assert.Equal("http://otel-collector:4318", otlpEndpoint);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", null);
        }
    }

    private sealed class LogCapture : IDisposable
    {
        private readonly StringBuilder _warnings = new();
        private readonly ILogger _logger;

        public LogCapture()
        {
            _logger = new TestLogger(output => _warnings.AppendLine(output));
        }

        public ILogger Logger => _logger;

        public List<string> GetWarnings()
        {
            return _warnings.ToString()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Contains("Ignored unknown configuration key"))
                .ToList();
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestLogger(Action<string> output) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                output(formatter(state, exception));
            }
        }
    }
}