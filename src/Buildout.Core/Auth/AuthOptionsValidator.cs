using Microsoft.Extensions.Options;

namespace Buildout.Core.Auth;

public sealed class AuthOptionsValidator : IValidateOptions<AuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        var errors = new List<string>();

        if (options.Mode == AuthMode.Proxy || options.Mode == AuthMode.Mapped)
        {
            if (string.IsNullOrWhiteSpace(options.Provider))
            {
                errors.Add($"Provider is required when AuthMode is {options.Mode}.");
            }
            else if (!string.Equals(options.Provider!, "sqlite", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(options.Provider!, "postgresql", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Provider must be 'sqlite' or 'postgresql' when AuthMode is {options.Mode}.");
            }
        }

        if (string.Equals(options.Provider, "sqlite", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(options.SqlitePath))
        {
            errors.Add("SqlitePath is required when Provider is 'sqlite'.");
        }

        if (string.Equals(options.Provider, "postgresql", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            errors.Add("ConnectionString is required when Provider is 'postgresql'.");
        }

        if (errors.Count > 0)
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}