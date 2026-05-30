using Buildout.Core.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Buildout.Mcp.Auth;

public sealed class NoneAuthenticator : IRequestAuthenticator
{
    private readonly string _globalBotToken;
    private readonly ILogger<NoneAuthenticator> _logger;

    public NoneAuthenticator(IConfiguration configuration, ILogger<NoneAuthenticator> logger)
    {
        _globalBotToken = configuration.GetValue<string>("BotToken") ?? string.Empty;
        _logger = logger;
    }

    public Task<AuthResult> AuthenticateAsync(string? authorizationHeader)
    {
        return Task.FromResult(AuthResult.Success(_globalBotToken, null));
    }
}