using Microsoft.Kiota.Abstractions.Authentication;

namespace Buildout.Core.Buildin.Authentication;

public sealed class ContextualTokenProvider : BaseBearerTokenAuthenticationProvider
{
    private static readonly AsyncLocal<string?> CurrentToken = new();
    private readonly string _defaultToken;

    private sealed class ContextualTokenAccessProvider : IAccessTokenProvider
    {
        private readonly string _defaultToken;

        public ContextualTokenAccessProvider(string defaultToken)
        {
            _defaultToken = defaultToken;
        }

        public AllowedHostsValidator AllowedHostsValidator { get; } = new();

        public Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CurrentToken.Value ?? _defaultToken);
        }
    }

    public ContextualTokenProvider(string defaultBotToken)
        : base(new ContextualTokenAccessProvider(defaultBotToken))
    {
        _defaultToken = defaultBotToken;
    }

    public static IDisposable OverrideToken(string token)
    {
        var previousToken = CurrentToken.Value;
        CurrentToken.Value = token;
        return new TokenScope(previousToken);
    }

    private sealed class TokenScope : IDisposable
    {
        private readonly string? _previousToken;

        public TokenScope(string? previousToken)
        {
            _previousToken = previousToken;
        }

        public void Dispose()
        {
            CurrentToken.Value = _previousToken;
        }
    }
}
