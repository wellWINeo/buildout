using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Buildout.Core.Configuration;

public static class BuildoutConfiguration
{
    private static readonly BuildoutConfigurationOptions DefaultOptions = new();

    public static (IConfiguration Configuration, string[] ResidualArgs) Build(string[] args)
    {
        return Build(args, DefaultOptions, null);
    }

    internal static (IConfiguration Configuration, string[] ResidualArgs) Build(
        string[] args,
        BuildoutConfigurationOptions options,
        ILogger? logger)
    {
        var (configPath, residualArgs) = ConfigFlagParser.Extract(args);

        var builder = new ConfigurationBuilder();

        string filePath;

        if (!string.IsNullOrEmpty(configPath))
        {
            filePath = configPath;

            if (!File.Exists(filePath))
            {
                throw new BuildoutConfigurationException(
                    $"Configuration file not found: {filePath}",
                    filePath);
            }

            if (Directory.Exists(filePath))
            {
                throw new BuildoutConfigurationException(
                    $"Configuration path is not a file: {filePath}",
                    filePath);
            }
        }
        else
        {
            filePath = options.DefaultFilePath;
        }

        if (!string.IsNullOrEmpty(filePath))
        {
            builder.AddJsonFile(filePath, optional: string.IsNullOrEmpty(configPath), reloadOnChange: false);
        }

        builder.Sources.Add(new LegacyOtelEndpointSource());
        builder.Sources.Add(new LegacyEnvVarSource());
        builder.AddEnvironmentVariables(options.Prefix);

        var baseConfigRoot = (IConfigurationRoot)builder.Build();

        var remapBuilder = new ConfigurationBuilder();
        remapBuilder.AddConfiguration(baseConfigRoot);
        remapBuilder.Sources.Add(new HttpSectionRemapSource(baseConfigRoot));

        var configRoot = (IConfigurationRoot)remapBuilder.Build();

        var auditLogger = logger ?? new FallbackLogger();
        UnknownKeyAuditor.Audit(configRoot, auditLogger);

        var botToken = configRoot["BotToken"];
        if (string.IsNullOrWhiteSpace(botToken))
        {
            var filePathStr = !string.IsNullOrEmpty(configPath) ? configPath : (!string.IsNullOrEmpty(options.DefaultFilePath) ? options.DefaultFilePath : "no configuration file");
            var message = $"BotToken is required. Set the {options.Prefix}BotToken environment variable, or provide it in {filePathStr}";
            throw new BuildoutConfigurationException(message, !string.IsNullOrEmpty(configPath) ? configPath : null);
        }

        return (configRoot, residualArgs);
    }

    private sealed class FallbackLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                Console.Error.WriteLine(formatter(state, exception));
            }
        }
    }
}