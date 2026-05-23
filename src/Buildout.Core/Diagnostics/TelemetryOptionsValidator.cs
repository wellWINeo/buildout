using Microsoft.Extensions.Options;

namespace Buildout.Core.Diagnostics;

public sealed class TelemetryOptionsValidator : IValidateOptions<TelemetryOptions>
{
    public ValidateOptionsResult Validate(string? name, TelemetryOptions options)
    {
        if (!options.OtlpEndpoint.IsAbsoluteUri)
            return ValidateOptionsResult.Fail("OtlpEndpoint must be an absolute URI.");

        if (!options.OtlpEndpoint.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
            !options.OtlpEndpoint.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            return ValidateOptionsResult.Fail("OtlpEndpoint must use HTTP or HTTPS.");

        return ValidateOptionsResult.Success;
    }
}