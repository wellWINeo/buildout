using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Buildout.Core.Buildin;

/// <summary>Resolves the primary bearer credential while preserving the legacy setting safely.</summary>
public sealed class AccessTokenResolver
{
#pragma warning disable CS0618
    private static int _legacyWarningEmitted;
    private readonly IOptions<BuildinClientOptions> _options;
    private readonly ILogger<AccessTokenResolver> _logger;

    public AccessTokenResolver(IOptions<BuildinClientOptions> options, ILogger<AccessTokenResolver> logger)
    {
        _options = options;
        _logger = logger;
    }

    public string Resolve()
    {
        var options = _options.Value;
        var primary = options.AccessToken?.Trim();
        var legacy = options.BotToken?.Trim();
        if (!string.IsNullOrWhiteSpace(primary))
        {
            if (!string.IsNullOrWhiteSpace(legacy)) WarnLegacy("AccessToken takes precedence and BotToken was ignored.");
            return primary;
        }

        if (!string.IsNullOrWhiteSpace(legacy))
        {
            WarnLegacy("BotToken is deprecated; configure AccessToken instead.");
            return legacy;
        }

        throw new InvalidOperationException("AccessToken is required.");
    }

    private void WarnLegacy(string message)
    {
        if (Interlocked.Exchange(ref _legacyWarningEmitted, 1) == 0)
            LegacyWarning(_logger, message, null);
    }

    private static readonly Action<ILogger, string, Exception?> LegacyWarning =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1601, "LegacyBuildinToken"), "{Message}");
#pragma warning restore CS0618
}
