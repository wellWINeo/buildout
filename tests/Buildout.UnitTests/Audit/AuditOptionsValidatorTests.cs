using Buildout.Core.Audit;
using Microsoft.Extensions.Options;
using Xunit;

namespace Buildout.UnitTests.Audit;

public class AuditOptionsValidatorTests
{
    private readonly AuditOptionsValidator _validator;

    public AuditOptionsValidatorTests()
    {
        _validator = new AuditOptionsValidator();
    }

    [Fact]
    public void Validate_ReturnsSuccessWhenDisabled()
    {
        var options = new AuditOptions { Enabled = false };
        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_FailsWhenEnabledWithoutProvider()
    {
        var options = new AuditOptions { Enabled = true };
        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains("Audit:Provider is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_FailsWhenProviderIsNotValid()
    {
        var options = new AuditOptions
        {
            Enabled = true,
            Provider = "invalid"
        };
        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains("must be either 'sqlite' or 'postgresql'", result.FailureMessage);
    }

    [Fact]
    public void Validate_FailsWhenSqliteProviderWithoutPath()
    {
        var options = new AuditOptions
        {
            Enabled = true,
            Provider = "sqlite"
        };
        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains("Audit:SqlitePath is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_SucceedsWhenSqliteProviderWithPath()
    {
        var options = new AuditOptions
        {
            Enabled = true,
            Provider = "sqlite",
            SqlitePath = "/path/to/db.sqlite"
        };
        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_FailsWhenPostgresProviderWithoutConnectionString()
    {
        var options = new AuditOptions
        {
            Enabled = true,
            Provider = "postgresql"
        };
        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains("Audit:ConnectionString is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_SucceedsWhenPostgresProviderWithConnectionString()
    {
        var options = new AuditOptions
        {
            Enabled = true,
            Provider = "postgresql",
            ConnectionString = "Host=localhost;Port=5432;Database=audit;Username=test;Password=test"
        };
        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_FailsWhenMaxParameterLengthIsZero()
    {
        var options = new AuditOptions
        {
            Enabled = true,
            Provider = "sqlite",
            SqlitePath = "/path/to/db.sqlite",
            MaxParameterLength = 0
        };
        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains("Audit:MaxParameterLength must be greater than 0", result.FailureMessage);
    }

    [Fact]
    public void Validate_FailsWhenMaxParameterLengthIsNegative()
    {
        var options = new AuditOptions
        {
            Enabled = true,
            Provider = "sqlite",
            SqlitePath = "/path/to/db.sqlite",
            MaxParameterLength = -1
        };
        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains("Audit:MaxParameterLength must be greater than 0", result.FailureMessage);
    }
}