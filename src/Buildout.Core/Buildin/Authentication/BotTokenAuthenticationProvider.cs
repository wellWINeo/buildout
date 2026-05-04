using Microsoft.Kiota.Abstractions.Authentication;

namespace Buildout.Core.Buildin.Authentication;

public sealed class BotTokenAuthenticationProvider : BaseBearerTokenAuthenticationProvider
{
    private sealed class BotTokenAccessProvider : IAccessTokenProvider
    {
        private readonly string _botToken;

        public BotTokenAccessProvider(string botToken)
        {
            _botToken = botToken;
        }

        public AllowedHostsValidator AllowedHostsValidator { get; } = new();

        public Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_botToken);
        }
    }

    public BotTokenAuthenticationProvider(string botToken)
        : base(new BotTokenAccessProvider(botToken))
    {
    }
}
