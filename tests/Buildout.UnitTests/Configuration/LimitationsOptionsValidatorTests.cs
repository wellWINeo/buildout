using Buildout.Core.Markdown.Editing;
using Xunit;

namespace Buildout.UnitTests.Configuration;

public sealed class LimitationsOptionsValidatorTests
{
    private readonly LimitationsOptionsValidator _validator = new();

    [Fact]
    public void LargeDeleteThreshold_GreaterThanOrEqualToZero_PassesValidation()
    {
        var options = new LimitationsOptions
        {
            LargeDeleteThreshold = 10
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void LargeDeleteThreshold_Zero_PassesValidation()
    {
        var options = new LimitationsOptions
        {
            LargeDeleteThreshold = 0
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void LargeDeleteThreshold_Negative_FailsValidation()
    {
        var options = new LimitationsOptions
        {
            LargeDeleteThreshold = -1
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("LargeDeleteThreshold", result.FailureMessage);
    }
}