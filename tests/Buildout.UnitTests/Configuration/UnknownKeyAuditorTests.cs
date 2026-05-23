using Buildout.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Configuration;

public class UnknownKeyAuditorTests
{
    [Fact]
    public void Audit_CanonicalKeysProduceNoWarning()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotToken"] = "token123",
                ["BaseUrl"] = "https://api.example.com",
                ["Http:Timeout"] = "00:01:00",
                ["Http:UnsafeAllowInsecure"] = "true",
                ["Telemetry:Enabled"] = "true",
                ["Telemetry:OtlpEndpoint"] = "http://localhost:4318",
                ["Limitations:LargeDeleteThreshold"] = "20"
            })
            .Build();

        var logger = Substitute.For<ILogger>();

        UnknownKeyAuditor.Audit(config, logger);

        logger.DidNotReceive().Log(Arg.Any<LogLevel>(), Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [InlineData("UnknownKey1")]
    [InlineData("UnknownKey2", "AnotherUnknownKey")]
    [InlineData("Some:Deeply:Nested:Key")]
    public void Audit_UnknownRootKeysEachYieldExactlyOneWarning(params string[] unknownKeys)
    {
        var configData = unknownKeys.ToDictionary(k => k, _ => (string?)"value");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var logger = Substitute.For<ILogger>();

        UnknownKeyAuditor.Audit(config, logger);

        logger.Received(unknownKeys.Length)
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Ignored unknown configuration key")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [InlineData("Buildin:BotToken", "BotToken")]
    [InlineData("Buildin:BaseUrl", "BaseUrl")]
    [InlineData("Buildin:HttpTimeout", "Http:Timeout")]
    [InlineData("Buildin:UnsafeAllowInsecure", "Http:UnsafeAllowInsecure")]
    [InlineData("PageEditor:LargeDeleteThreshold", "Limitations:LargeDeleteThreshold")]
    [InlineData("BUILDOUT_TELEMETRY_ENABLED", "Telemetry:Enabled")]
    public void Audit_LegacyKeysProduceWarningThatNamesReplacement(string legacyKey, string replacementKey)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [legacyKey] = "value"
            })
            .Build();

        var logger = Substitute.For<ILogger>();

        UnknownKeyAuditor.Audit(config, logger);

        logger.Received(1)
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o =>
                    o.ToString()!.Contains(legacyKey) &&
                    o.ToString()!.Contains($"Use '{replacementKey}' instead")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void Audit_OtelExporterEndpointProducesNoWarning()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4318"
            })
            .Build();

        var logger = Substitute.For<ILogger>();

        UnknownKeyAuditor.Audit(config, logger);

        logger.DidNotReceive().Log(Arg.Any<LogLevel>(), Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void Audit_MixedLegacyAndUnknownKeysProduceCorrectWarnings()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Buildin:BotToken"] = "token123",
                ["UnknownKey"] = "value",
                ["Telemetry:Enabled"] = "true",
                ["BUILDOUT_TELEMETRY_ENABLED"] = "true"
            })
            .Build();

        var logger = Substitute.For<ILogger>();

        UnknownKeyAuditor.Audit(config, logger);

        logger.Received(1)
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Buildin:BotToken") && o.ToString()!.Contains("Use 'BotToken' instead")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());

        logger.Received(1)
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("BUILDOUT_TELEMETRY_ENABLED") && o.ToString()!.Contains("Use 'Telemetry:Enabled' instead")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());

        logger.Received(1)
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("UnknownKey") && !o.ToString()!.Contains("Use")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
    }
}