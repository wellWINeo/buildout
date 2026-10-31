using Buildout.Core.Buildin.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace Buildout.Core.Buildin;

/// <summary>
/// Compatibility name for callers and tests that still construct the legacy Kiota client.
/// New application code should use <see cref="BuildinClient"/>.
/// </summary>
[Obsolete("Use BuildinClient, which targets Buildin Developer API V2.")]
public sealed class BotBuildinClient : LegacyBuildinClient
{
    public BotBuildinClient(IRequestAdapter requestAdapter, IOptions<BuildinClientOptions> options, ILogger<BotBuildinClient> logger)
        : base(requestAdapter, options, logger)
    {
    }

    public BotBuildinClient(HttpClient httpClient, IAuthenticationProvider authProvider, IOptions<BuildinClientOptions> options, ILogger<BotBuildinClient> logger)
        : base(httpClient, authProvider, options, logger)
    {
    }
}
