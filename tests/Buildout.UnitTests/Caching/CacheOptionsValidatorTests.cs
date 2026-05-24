using Buildout.Core.Caching;
using Xunit;

namespace Buildout.UnitTests.Caching;

public sealed class CacheOptionsValidatorTests
{
    private readonly CacheOptionsValidator _validator = new();

    [Fact]
    public void EnabledTrue_MaxEntriesGreaterThanZero_PassesValidation()
    {
        var options = new CacheOptions
        {
            Enabled = true,
            MaxEntries = 50
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void EnabledTrue_MaxEntriesOne_PassesValidation()
    {
        var options = new CacheOptions
        {
            Enabled = true,
            MaxEntries = 1
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void EnabledTrue_MaxEntriesZero_FailsValidation()
    {
        var options = new CacheOptions
        {
            Enabled = true,
            MaxEntries = 0
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("MaxEntries", result.FailureMessage);
    }

    [Fact]
    public void EnabledTrue_MaxEntriesNegative_FailsValidation()
    {
        var options = new CacheOptions
        {
            Enabled = true,
            MaxEntries = -1
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("MaxEntries", result.FailureMessage);
    }

    [Fact]
    public void EnabledFalse_MaxEntriesZero_PassesValidation()
    {
        var options = new CacheOptions
        {
            Enabled = false,
            MaxEntries = 0
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void EnabledFalse_MaxEntriesNegative_PassesValidation()
    {
        var options = new CacheOptions
        {
            Enabled = false,
            MaxEntries = -1
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }
}