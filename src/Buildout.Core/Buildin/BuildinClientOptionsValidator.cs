using Microsoft.Extensions.Options;

namespace Buildout.Core.Buildin;

public sealed class BuildinClientOptionsValidator : IValidateOptions<BuildinClientOptions>
{
#pragma warning disable CS0618
    public ValidateOptionsResult Validate(string? name, BuildinClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AccessToken) && string.IsNullOrWhiteSpace(options.BotToken))
            return ValidateOptionsResult.Fail("AccessToken is required. Set the Buildout__AccessToken environment variable (BotToken is accepted only as a deprecated fallback via Buildout__BotToken).");

        if (options.BaseUrl is null || !options.BaseUrl.IsAbsoluteUri)
            return ValidateOptionsResult.Fail("BaseUrl must be an absolute URI.");

        if (!options.BaseUrl.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) && !options.Http.UnsafeAllowInsecure)
            return ValidateOptionsResult.Fail("BaseUrl must use HTTPS. Set Http:UnsafeAllowInsecure to allow HTTP.");

        if (options.Http.Timeout <= TimeSpan.Zero)
            return ValidateOptionsResult.Fail("Http:Timeout must be a positive duration.");

        return ValidateOptionsResult.Success;
    }
#pragma warning restore CS0618
}
