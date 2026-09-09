using Microsoft.Kiota.Abstractions.Authentication;

namespace Buildout.Core.Buildin.Authentication;

public sealed class AccessTokenAuthenticationProvider : BaseBearerTokenAuthenticationProvider
{
    private sealed class Provider : IAccessTokenProvider
    {
        private readonly string _token;
        private readonly string? _allowedHost;

        public Provider(string token, string? allowedHost)
        {
            _token = token;
            _allowedHost = allowedHost;
        }

        public AllowedHostsValidator AllowedHostsValidator { get; } = new();

        public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            if (_allowedHost is not null && !string.Equals(_allowedHost, uri.Host, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(string.Empty);
            return Task.FromResult(_token);
        }
    }

    public AccessTokenAuthenticationProvider(string token, IEnumerable<string>? allowedHosts = null)
        : base(new Provider(token, allowedHosts?.FirstOrDefault())) { }
}
