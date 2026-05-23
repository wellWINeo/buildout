using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Buildout.Core.Configuration;

public static class UnknownKeyAuditor
{
    private static readonly HashSet<string> CanonicalKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "BotToken",
        "BaseUrl",
        "Http:Timeout",
        "Http:UnsafeAllowInsecure",
        "Limitations:LargeDeleteThreshold",
        "Telemetry:Enabled",
        "Telemetry:OtlpEndpoint"
    };

    private static readonly Dictionary<string, string> LegacyKeyHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Buildin:BotToken"] = "BotToken",
        ["Buildin:BaseUrl"] = "BaseUrl",
        ["Buildin:HttpTimeout"] = "Http:Timeout",
        ["Buildin:UnsafeAllowInsecure"] = "Http:UnsafeAllowInsecure",
        ["PageEditor:LargeDeleteThreshold"] = "Limitations:LargeDeleteThreshold",
        ["BUILDOUT_TELEMETRY_ENABLED"] = "Telemetry:Enabled"
    };

    private static readonly Dictionary<string, string> EnvVarNameHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Buildout:Telemetry:Enabled"] = "BUILDOUT_TELEMETRY_ENABLED"
    };

    private static readonly HashSet<string> IgnoredLegacyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "OTEL_EXPORTER_OTLP_ENDPOINT"
    };

    public static void Audit(IConfiguration configuration, ILogger logger)
    {
        var loadedKeys = configuration.GetChildren()
            .Select(child => child.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in loadedKeys)
        {
            if (IgnoredLegacyKeys.Contains(key))
                continue;

            if (CanonicalKeys.Contains(key))
                continue;

            if (LegacyKeyHints.TryGetValue(key, out var replacement))
            {
                var envVarName = EnvVarNameHints.TryGetValue(key, out var envVar) ? envVar : replacement.Replace(":", "__");
                logger.LogWarning(
                    "[buildout-config] Ignored unknown configuration key '{Key}' (env var '{EnvVarName}'). Use '{Replacement}' instead (or env var 'Buildout__{ReplacementEnv}'). See docs/configuration.md \"Migration from earlier versions\".",
                    key,
                    envVarName,
                    replacement,
                    replacement.Replace(":", "__"));
            }
            else
            {
                logger.LogWarning(
                    "[buildout-config] Ignored unknown configuration key '{Key}'. See docs/configuration.md for the supported schema.",
                    key);
            }
        }
    }
}