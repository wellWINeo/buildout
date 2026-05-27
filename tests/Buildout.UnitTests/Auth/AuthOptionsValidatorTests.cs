using Xunit;
using Microsoft.Extensions.Options;

namespace Buildout.Core.Auth.Tests;

public sealed class AuthOptionsValidatorTests
{
    private readonly AuthOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidNoneMode_ReturnsSuccess()
    {
        var options = new AuthOptions { Mode = AuthMode.None };
        var result = _validator.Validate(nameof(AuthOptions), options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ValidPassthroughMode_ReturnsSuccess()
    {
        var options = new AuthOptions { Mode = AuthMode.Passthrough };
        var result = _validator.Validate(nameof(AuthOptions), options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ProxyModeWithMissingProvider_ReturnsFailure()
    {
        var options = new AuthOptions { Mode = AuthMode.Proxy };
        var result = _validator.Validate(nameof(AuthOptions), options);

        Assert.False(result.Succeeded);
        Assert.Contains("Provider is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_ProxyModeWithInvalidProvider_ReturnsFailure()
    {
        var options = new AuthOptions { Mode = AuthMode.Proxy, Provider = "mysql" };
        var result = _validator.Validate(nameof(AuthOptions), options);

        Assert.False(result.Succeeded);
        Assert.Contains("Provider must be 'sqlite' or 'postgresql'", result.FailureMessage);
    }

    [Fact]
    public void Validate_ProxyModeWithSqliteProviderAndMissingPath_ReturnsFailure()
    {
        var options = new AuthOptions { Mode = AuthMode.Proxy, Provider = "sqlite" };
        var result = _validator.Validate(nameof(AuthOptions), options);

        Assert.False(result.Succeeded);
        Assert.Contains("SqlitePath is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_ProxyModeWithSqliteProviderAndValidPath_ReturnsSuccess()
    {
        var options = new AuthOptions { Mode = AuthMode.Proxy, Provider = "sqlite", SqlitePath = "/path/to/db" };
        var result = _validator.Validate(nameof(AuthOptions), options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ProxyModeWithPostgresqlProviderAndMissingConnectionString_ReturnsFailure()
    {
        var options = new AuthOptions { Mode = AuthMode.Proxy, Provider = "postgresql" };
        var result = _validator.Validate(nameof(AuthOptions), options);

        Assert.False(result.Succeeded);
        Assert.Contains("ConnectionString is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_ProxyModeWithPostgresqlProviderAndValidConnectionString_ReturnsSuccess()
    {
        var options = new AuthOptions
        {
            Mode = AuthMode.Proxy,
            Provider = "postgresql",
            ConnectionString = "Host=localhost;Database=buildout"
        };
        var result = _validator.Validate(nameof(AuthOptions), options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MappedModeWithSqliteProviderAndValidPath_ReturnsSuccess()
    {
        var options = new AuthOptions { Mode = AuthMode.Mapped, Provider = "sqlite", SqlitePath = "/path/to/db" };
        var result = _validator.Validate(nameof(AuthOptions), options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ProviderCaseInsensitive_ReturnsSuccess()
    {
        var options = new AuthOptions { Mode = AuthMode.Proxy, Provider = "SQLite", SqlitePath = "/path/to/db" };
        var result = _validator.Validate(nameof(AuthOptions), options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_NoneModeIgnoresProviderFields_ReturnsSuccess()
    {
        var options = new AuthOptions { Mode = AuthMode.None, Provider = "invalid", SqlitePath = null, ConnectionString = null };
        var result = _validator.Validate(nameof(AuthOptions), options);

        Assert.True(result.Succeeded);
    }
}