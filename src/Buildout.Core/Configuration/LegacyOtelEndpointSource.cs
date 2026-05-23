using Microsoft.Extensions.Configuration;

namespace Buildout.Core.Configuration;

public sealed class LegacyOtelEndpointSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new LegacyOtelEndpointProvider();
    }

    private sealed class LegacyOtelEndpointProvider : ConfigurationProvider
    {
        public override void Load()
        {
            var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

            if (!string.IsNullOrEmpty(endpoint))
            {
                Data["Telemetry:OtlpEndpoint"] = endpoint;
            }
        }
    }
}