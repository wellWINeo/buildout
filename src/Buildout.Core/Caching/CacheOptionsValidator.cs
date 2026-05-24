using Microsoft.Extensions.Options;

namespace Buildout.Core.Caching;

/// <summary>
/// Validates <see cref="CacheOptions"/> to ensure configuration is correct.
/// </summary>
public sealed class CacheOptionsValidator : IValidateOptions<CacheOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, CacheOptions options)
    {
        if (options.Enabled && options.MaxEntries <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"Cache is enabled but MaxEntries ({options.MaxEntries}) must be greater than 0.");
        }

        return ValidateOptionsResult.Success;
    }
}