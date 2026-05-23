using Microsoft.Extensions.Configuration;

namespace Buildout.Configuration;

public static class BuildoutConfiguration
{
    private static readonly BuildoutConfigurationOptions DefaultOptions = new();

    public static IConfiguration Build(string? configPath = null)
    {
        return Build(configPath, DefaultOptions);
    }

    internal static IConfiguration Build(string? configPath, BuildoutConfigurationOptions options)
    {
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

        builder.AddEnvironmentVariables(options.Prefix);

        var configRoot = (IConfigurationRoot)builder.Build();

        var botToken = configRoot["BotToken"];
        if (string.IsNullOrWhiteSpace(botToken))
        {
            var filePathStr = !string.IsNullOrEmpty(configPath) ? configPath
                : (!string.IsNullOrEmpty(options.DefaultFilePath) ? options.DefaultFilePath : "no configuration file");
            var message = $"BotToken is required. Set the {options.Prefix}BotToken environment variable, or provide it in {filePathStr}";
            throw new BuildoutConfigurationException(message, !string.IsNullOrEmpty(configPath) ? configPath : null);
        }

        return configRoot;
    }
}
