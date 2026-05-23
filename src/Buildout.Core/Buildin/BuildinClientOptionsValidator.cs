using Microsoft.Extensions.Options;

namespace Buildout.Core.Buildin;

public sealed class BuildinClientOptionsValidator : IValidateOptions<BuildinClientOptions>
{
    public ValidateOptionsResult Validate(string? name, BuildinClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BotToken))
            return ValidateOptionsResult.Fail("BotToken is required.");

        if (options.BaseUrl is null || !options.BaseUrl.IsAbsoluteUri)
            return ValidateOptionsResult.Fail("BaseUrl must be an absolute URI.");

        if (!options.BaseUrl.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) && !options.UnsafeAllowInsecure)
            return ValidateOptionsResult.Fail("BaseUrl must use HTTPS. Set Http:UnsafeAllowInsecure to allow HTTP.");

        if (options.HttpTimeout <= TimeSpan.Zero)
            return ValidateOptionsResult.Fail("HttpTimeout must be a positive duration.");

        return ValidateOptionsResult.Success;
    }
}
