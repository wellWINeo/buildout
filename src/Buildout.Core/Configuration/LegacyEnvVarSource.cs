using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Buildout.Core.Configuration;

public sealed class LegacyEnvVarSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new LegacyEnvVarProvider();
    }

    private sealed class LegacyEnvVarProvider : IConfigurationProvider
    {
        private static readonly string[] LegacyKeys = [
            "Buildin:BotToken",
            "Buildin:BaseUrl",
            "Buildin:HttpTimeout",
            "Buildin:UnsafeAllowInsecure",
            "PageEditor:LargeDeleteThreshold",
            "Telemetry:Enabled"
        ];

        public bool TryGet(string key, out string? value)
        {
            value = key switch
            {
                "Buildin:BotToken" => Environment.GetEnvironmentVariable("BUILDOUT_BOT_TOKEN"),
                "Buildin:BaseUrl" => Environment.GetEnvironmentVariable("BUILDOUT_BASE_URL"),
                "Buildin:HttpTimeout" => Environment.GetEnvironmentVariable("BUILDOUT_HTTP_TIMEOUT"),
                "Buildin:UnsafeAllowInsecure" => Environment.GetEnvironmentVariable("BUILDOUT_UNSAFE_ALLOW_INSECURE"),
                "PageEditor:LargeDeleteThreshold" => Environment.GetEnvironmentVariable("BUILDOUT_PAGE_EDITOR_LARGE_DELETE_THRESHOLD"),
                "Telemetry:Enabled" => Environment.GetEnvironmentVariable("BUILDOUT_TELEMETRY_ENABLED"),
                _ => null
            };
            return value != null;
        }

        public void Set(string key, string? value)
        {
        }

        public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string? parentPath)
        {
            return LegacyKeys.Except(earlierKeys ?? Array.Empty<string>());
        }

        public IChangeToken GetReloadToken()
        {
            return new ChangeToken();
        }

        public void Load()
        {
        }

        private sealed class ChangeToken : IChangeToken
        {
            public bool HasChanged => false;

            public bool ActiveChangeCallbacks => false;

            public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
            {
                return Disposable.Empty;
            }
        }

        private sealed class Disposable : IDisposable
        {
            public static readonly Disposable Empty = new();

            private Disposable() { }

            public void Dispose()
            {
            }
        }
    }
}
