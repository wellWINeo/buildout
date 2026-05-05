using Microsoft.Extensions.Options;

namespace Buildout.Core.Buildin;

public sealed class BuildinClientOptionsValidator : IValidateOptions<BuildinClientOptions>
{
    public ValidateOptionsResult Validate(string? name, BuildinClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BotToken))
            return ValidateOptionsResult.Fail("BuildinClientOptions.BotToken is required.");

        if (options.BaseUrl is null || !options.BaseUrl.IsAbsoluteUri)
            return ValidateOptionsResult.Fail("BuildinClientOptions.BaseUrl must be an absolute URI.");

        if (!options.BaseUrl.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) && !options.UnsafeAllowInsecure)
            return ValidateOptionsResult.Fail("BuildinClientOptions.BaseUrl must use HTTPS. Set UnsafeAllowInsecure to allow HTTP.");

        if (options.HttpTimeout <= TimeSpan.Zero)
            return ValidateOptionsResult.Fail("BuildinClientOptions.HttpTimeout must be a positive duration.");

        return ValidateOptionsResult.Success;
    }
}
