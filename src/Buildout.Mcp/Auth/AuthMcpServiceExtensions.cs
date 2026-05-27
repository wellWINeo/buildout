using Buildout.Core.Audit;
using Buildout.Core.Auth;
using Buildout.Mcp.Audit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

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

        services.AddSingleton<IValidateOptions<AuthOptions>, Buildout.Core.Auth.AuthOptionsValidator>();

        var authModeEnum = configuration.GetValue<string>("Auth:Mode");
        var authMode = string.IsNullOrEmpty(authModeEnum) ? AuthMode.None : Enum.Parse<AuthMode>(authModeEnum, true);

        if (isHttpTransport)
        {
            services.AddHttpContextAccessor();

            IRequestAuthenticator authenticator = authMode switch
            {
                AuthMode.None => new NoneAuthenticator(configuration, services.BuildServiceProvider().GetRequiredService<ILogger<NoneAuthenticator>>()),
                AuthMode.Passthrough => new PassthroughAuthenticator(services.BuildServiceProvider().GetRequiredService<ILogger<PassthroughAuthenticator>>()),
                _ => throw new InvalidOperationException("Proxy and Mapped modes require database initialization")
            };

            services.AddSingleton(authenticator);
            services.AddSingleton<Microsoft.Extensions.Options.IConfigureOptions<ModelContextProtocol.Server.McpServerOptions>, AuthFilter>();

            if (authMode is AuthMode.Proxy or AuthMode.Mapped)
            {
                var provider = configuration.GetValue<string>("Auth:Provider");
                var serviceProvider = services.BuildServiceProvider();
                var logger = serviceProvider.GetRequiredService<ILogger<AdoNetTokenStore>>();

                if (provider == "sqlite")
                {
                    var sqlitePath = configuration.GetValue<string>("Auth:SqlitePath");
                    var connectionString = $"Data Source={sqlitePath}";
                    services.AddSingleton<ITokenStore>(new AdoNetTokenStore(connectionString, "sqlite", logger));
                }
                else if (provider == "postgresql")
                {
                    var connectionString = configuration.GetValue<string>("Auth:ConnectionString");
                    services.AddSingleton<ITokenStore>(new AdoNetTokenStore(connectionString!, "postgresql", logger));
                }

                authenticator = authMode == AuthMode.Proxy
                    ? new ProxyAuthenticator(configuration, serviceProvider.GetRequiredService<ITokenStore>(), serviceProvider.GetRequiredService<ILogger<ProxyAuthenticator>>())
                    : new MappedAuthenticator(serviceProvider.GetRequiredService<ITokenStore>(), serviceProvider.GetRequiredService<ILogger<MappedAuthenticator>>());

                services.AddSingleton(authenticator);
            }
        }

        return services;
    }

    public static bool AuthNeedsDb(IConfiguration configuration)
    {
        var authModeEnum = configuration.GetValue<string>("Auth:Mode");
        var authMode = string.IsNullOrEmpty(authModeEnum) ? AuthMode.None : Enum.Parse<AuthMode>(authModeEnum, true);
        return authMode is AuthMode.Proxy or AuthMode.Mapped;
    }
}