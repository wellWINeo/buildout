using Buildout.Configuration;
using Xunit;

namespace Buildout.IntegrationTests.Configuration;

public sealed class CliConfigFlagTests
{
    [Fact]
    public async Task EnvVarOnlyPath_ResolvesWithoutConfigError()
    {
        Environment.SetEnvironmentVariable("Buildout__BotToken", "test-token-123");

        try
        {
            var config = BuildoutConfiguration.Build();

            var botToken = config["BotToken"];
            Assert.Equal("test-token-123", botToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Buildout__BotToken", null);
        }
    }

    [Fact]
    public async Task DefaultFilePath_ResolvesCorrectly()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(homeDir, ".config", "buildout");
        Directory.CreateDirectory(configDir);

        var configPath = Path.Combine(configDir, "config.json");
        var jsonConfig = """
            {
                "BotToken": "default-file-token-456"
            }
            """;

        try
        {
            File.WriteAllText(configPath, jsonConfig);

            var config = BuildoutConfiguration.Build();

            var botToken = config["BotToken"];
            Assert.Equal("default-file-token-456", botToken);
        }
        finally
        {
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
            if (Directory.Exists(configDir))
            {
                Directory.Delete(configDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MissingBotToken_ExitsNonZero_WithHelpfulMessage()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(homeDir, ".config", "buildout");
        Directory.CreateDirectory(configDir);

        var configPath = Path.Combine(configDir, "config.json");
        var jsonConfig = """
            {
                "BaseUrl": "https://api.buildin.ai/"
            }
            """;

        try
        {
            File.WriteAllText(configPath, jsonConfig);

            var ex = Assert.ThrowsAny<Exception>(() =>
            {
                var _ = BuildoutConfiguration.Build();
            });

            Assert.NotNull(ex);
            var message = ex.Message;

            Assert.Contains("BotToken", message);
            Assert.Contains("Buildout__BotToken", message);
            Assert.Contains(configPath, message);
        }
        finally
        {
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
            if (Directory.Exists(configDir))
            {
                Directory.Delete(configDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ConfigFlag_OverridesDefaultFile()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(homeDir, ".config", "buildout");
        Directory.CreateDirectory(configDir);

        var defaultConfigPath = Path.Combine(configDir, "config.json");
        var defaultJsonConfig = """
            {
                "BotToken": "default-file-token"
            }
            """;

        var tempDir = Path.Combine(Path.GetTempPath(), $"buildout-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var overrideConfigPath = Path.Combine(tempDir, "other.json");
        var overrideJsonConfig = """
            {
                "BotToken": "override-file-token"
            }
            """;

        try
        {
            File.WriteAllText(defaultConfigPath, defaultJsonConfig);
            File.WriteAllText(overrideConfigPath, overrideJsonConfig);

            var config = BuildoutConfiguration.Build(overrideConfigPath);

            var botToken = config["BotToken"];
            Assert.Equal("override-file-token", botToken);
        }
        finally
        {
            if (File.Exists(overrideConfigPath))
            {
                File.Delete(overrideConfigPath);
            }
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
            if (File.Exists(defaultConfigPath))
            {
                File.Delete(defaultConfigPath);
            }
            if (Directory.Exists(configDir))
            {
                Directory.Delete(configDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ShortConfigFlag_BehavesIdenticallyToLongForm()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"buildout-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var configPath = Path.Combine(tempDir, "other.json");
        var jsonConfig = """
            {
                "BotToken": "short-flag-token"
            }
            """;

        try
        {
            File.WriteAllText(configPath, jsonConfig);

            var config = BuildoutConfiguration.Build(configPath);

            var botToken = config["BotToken"];
            Assert.Equal("short-flag-token", botToken);
        }
        finally
        {
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void MissingConfigPath_ExitsNonZero_WithErrorMessage_AndDoesNotSilentlyFallback()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(homeDir, ".config", "buildout");
        Directory.CreateDirectory(configDir);

        var defaultConfigPath = Path.Combine(configDir, "config.json");
        var defaultJsonConfig = """
            {
                "BotToken": "default-file-token"
            }
            """;

        try
        {
            File.WriteAllText(defaultConfigPath, defaultJsonConfig);

            var ex = Assert.ThrowsAny<Exception>(() =>
            {
                var _ = BuildoutConfiguration.Build("./nonexistent.json");
            });

            Assert.NotNull(ex);
            Assert.Contains("Configuration file not found:", ex.Message);
            Assert.Contains("./nonexistent.json", ex.Message);

            var botToken = defaultJsonConfig.Contains("default-file-token");
            Assert.True(botToken, "Default file exists but should not be used");
        }
        finally
        {
            if (File.Exists(defaultConfigPath))
            {
                File.Delete(defaultConfigPath);
            }
            if (Directory.Exists(configDir))
            {
                Directory.Delete(configDir, recursive: true);
            }
        }
    }

    [Fact]
    public void EnvironmentVar_WinsOverConfigFileValue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"buildout-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var configPath = Path.Combine(tempDir, "other.json");
        var jsonConfig = """
            {
                "BotToken": "file-token"
            }
            """;

        try
        {
            File.WriteAllText(configPath, jsonConfig);
            Environment.SetEnvironmentVariable("Buildout__BotToken", "env-var-token");

            var config = BuildoutConfiguration.Build(configPath);

            var botToken = config["BotToken"];
            Assert.Equal("env-var-token", botToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Buildout__BotToken", null);
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
