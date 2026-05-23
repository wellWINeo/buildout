using Buildout.Core.Diagnostics;
using Xunit;

namespace Buildout.UnitTests.Configuration;

public sealed class TelemetryOptionsValidatorTests
{
    private readonly TelemetryOptionsValidator _validator = new();

    [Fact]
    public void OtlpEndpoint_AbsoluteUri_Http_PassesValidation()
    {
        var options = new TelemetryOptions
        {
            OtlpEndpoint = new Uri("http://localhost:4318")
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void OtlpEndpoint_AbsoluteUri_Https_PassesValidation()
    {
        var options = new TelemetryOptions
        {
            OtlpEndpoint = new Uri("https://otel-collector.example.com:4318")
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void OtlpEndpoint_NonAbsoluteUri_FailsValidation()
    {
        var options = new TelemetryOptions
        {
            OtlpEndpoint = new Uri("/relative/path", UriKind.Relative)
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void OtlpEndpoint_FtpScheme_FailsValidation()
    {
        var options = new TelemetryOptions
        {
            OtlpEndpoint = new Uri("ftp://example.com:4318")
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void OtlpEndpoint_FileScheme_FailsValidation()
    {
        var options = new TelemetryOptions
        {
            OtlpEndpoint = new Uri("file:///path/to/file")
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Enabled_True_PassesValidation()
    {
        var options = new TelemetryOptions
        {
            Enabled = true,
            OtlpEndpoint = new Uri("http://localhost:4318")
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Enabled_False_PassesValidation()
    {
        var options = new TelemetryOptions
        {
            Enabled = false,
            OtlpEndpoint = new Uri("http://localhost:4318")
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }
}