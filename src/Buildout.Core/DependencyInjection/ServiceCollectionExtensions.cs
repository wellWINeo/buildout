using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions.Authentication;

namespace Buildout.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBuildinClient(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new BuildinClientOptions();
        configuration.GetSection("Buildin").Bind(options);

        var validator = new BuildinClientOptionsValidator();
        var validationResult = validator.Validate(null, options);
        if (validationResult.Failed)
            throw new InvalidOperationException(validationResult.FailureMessage);

        services.AddSingleton(options);
        services.AddSingleton<IValidateOptions<BuildinClientOptions>>(_ => validator);
        services.AddSingleton<IAuthenticationProvider>(sp =>
        {
            var opts = sp.GetRequiredService<BuildinClientOptions>();
            return new BotTokenAuthenticationProvider(opts.BotToken);
        });
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<BuildinClientOptions>();
            return new HttpClient
            {
                BaseAddress = opts.BaseUrl,
                Timeout = opts.HttpTimeout
            };
        });

        return services;
    }
}
