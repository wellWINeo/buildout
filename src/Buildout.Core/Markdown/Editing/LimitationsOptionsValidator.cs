using Microsoft.Extensions.Options;

namespace Buildout.Core.Markdown.Editing;

public sealed class LimitationsOptionsValidator : IValidateOptions<LimitationsOptions>
{
    public ValidateOptionsResult Validate(string? name, LimitationsOptions options)
    {
        if (options.LargeDeleteThreshold < 0)
            return ValidateOptionsResult.Fail("LimitationsOptions.LargeDeleteThreshold must be greater than or equal to zero.");

        return ValidateOptionsResult.Success;
    }
}