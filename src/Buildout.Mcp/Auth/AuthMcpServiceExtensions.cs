using Buildout.Core.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Buildout.Mcp.Auth;

public static class AuthMcpServiceExtensions
{
    public static IServiceCollection AddAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isHttpTransport)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection("Auth"))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();

        if (!isHttpTransport)
            return services;

        services.AddHttpContextAccessor();

        var authModeStr = configuration.GetValue<string>("Auth:Mode");
        var authMode = string.IsNullOrEmpty(authModeStr)
            ? AuthMode.None
            : Enum.Parse<AuthMode>(authModeStr, true);

        var provider = configuration.GetValue<string>("Auth:Provider")?.ToLowerInvariant();

        if (authMode is AuthMode.Proxy or AuthMode.Mapped)
        {
            if (provider == "sqlite")
            {
                var sqlitePath = configuration.GetValue<string>("Auth:SqlitePath");
                var cs = $"Data Source={sqlitePath}";
                services.AddSingleton<ITokenStore>(sp =>
                    new AdoNetTokenStore(cs, "sqlite",
                        sp.GetRequiredService<ILogger<AdoNetTokenStore>>()));
            }
            else if (provider == "postgresql")
            {
                var cs = configuration.GetValue<string>("Auth:ConnectionString")!;
                services.AddSingleton<ITokenStore>(sp =>
                    new AdoNetTokenStore(cs, "postgresql",
                        sp.GetRequiredService<ILogger<AdoNetTokenStore>>()));
            }
        }

        switch (authMode)
        {
            case AuthMode.Passthrough:
                services.AddSingleton<IRequestAuthenticator, PassthroughAuthenticator>();
                break;
            case AuthMode.Proxy:
                services.AddSingleton<IRequestAuthenticator, ProxyAuthenticator>();
                break;
            case AuthMode.Mapped:
                services.AddSingleton<IRequestAuthenticator, MappedAuthenticator>();
                break;
            default:
                services.AddSingleton<IRequestAuthenticator, NoneAuthenticator>();
                break;
        }

        services.AddSingleton<IConfigureOptions<ModelContextProtocol.Server.McpServerOptions>, AuthFilter>();
        return services;
    }

    public static bool AuthNeedsDb(IConfiguration configuration)
    {
        var authModeEnum = configuration.GetValue<string>("Auth:Mode");
        var authMode = string.IsNullOrEmpty(authModeEnum) ? AuthMode.None : Enum.Parse<AuthMode>(authModeEnum, true);
        return authMode is AuthMode.Proxy or AuthMode.Mapped;
    }
}
