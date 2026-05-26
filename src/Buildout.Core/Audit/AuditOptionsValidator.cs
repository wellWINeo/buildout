using Microsoft.Extensions.Options;

namespace Buildout.Core.Audit;

public class AuditOptionsValidator : IValidateOptions<AuditOptions>
{
    public ValidateOptionsResult Validate(string? name, AuditOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrEmpty(options.Provider))
        {
            return ValidateOptionsResult.Fail("Audit:Provider is required when Audit:Enabled is true.");
        }

        if (options.Provider != "sqlite" && options.Provider != "postgresql")
        {
            return ValidateOptionsResult.Fail("Audit:Provider must be either 'sqlite' or 'postgresql'.");
        }

        if (options.Provider == "sqlite" && string.IsNullOrEmpty(options.SqlitePath))
        {
            return ValidateOptionsResult.Fail("Audit:SqlitePath is required when Provider is 'sqlite'.");
        }

        if (options.Provider == "postgresql" && string.IsNullOrEmpty(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("Audit:ConnectionString is required when Provider is 'postgresql'.");
        }

        if (options.MaxParameterLength <= 0)
        {
            return ValidateOptionsResult.Fail("Audit:MaxParameterLength must be greater than 0.");
        }

        return ValidateOptionsResult.Success;
    }
}