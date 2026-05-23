using Buildout.Configuration;
using Xunit;

namespace Buildout.IntegrationTests.Configuration;

public sealed class McpConfigFlagTests
{
    [Fact]
    public void EnvVarOnlyMcpStartup_BindsBuildinClientOptionsBotTokenCorrectly()
    {
        Environment.SetEnvironmentVariable("Buildout__BotToken", "mcp-test-token-789");

        try
        {
            var config = BuildoutConfiguration.Build();

            var botToken = config["BotToken"];
            Assert.Equal("mcp-test-token-789", botToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Buildout__BotToken", null);
        }
    }

    [Fact]
    public void BasicMcpStartup_SucceedsWithMinimalConfig()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(homeDir, ".config", "buildout");
        Directory.CreateDirectory(configDir);

        var configPath = Path.Combine(configDir, "config.json");
        var jsonConfig = """
            {
                "BotToken": "minimal-mcp-token"
            }
            """;

        try
        {
            File.WriteAllText(configPath, jsonConfig);

            var config = BuildoutConfiguration.Build();

            var botToken = config["BotToken"];
            Assert.Equal("minimal-mcp-token", botToken);
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
    public void ShortConfigFlag_LoadsSpecifiedFileAndIgnoresDefault()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(homeDir, ".config", "buildout");
        Directory.CreateDirectory(configDir);

        var defaultConfigPath = Path.Combine(configDir, "config.json");
        var defaultJsonConfig = """
            {
                "BotToken": "default-mcp-token"
            }
            """;

        var tempDir = Path.Combine(Path.GetTempPath(), $"buildout-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var mcpConfigPath = Path.Combine(tempDir, "mcp-prod.json");
        var mcpJsonConfig = """
            {
                "BotToken": "mcp-prod-token"
            }
            """;

        try
        {
            File.WriteAllText(defaultConfigPath, defaultJsonConfig);
            File.WriteAllText(mcpConfigPath, mcpJsonConfig);

            var config = BuildoutConfiguration.Build(mcpConfigPath);

            var botToken = config["BotToken"];
            Assert.Equal("mcp-prod-token", botToken);
        }
        finally
        {
            if (File.Exists(mcpConfigPath))
            {
                File.Delete(mcpConfigPath);
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
    public void MissingConfigPath_ExitsNonZero_WithErrorMessage()
    {
        var ex = Assert.ThrowsAny<Exception>(() =>
        {
            var _ = BuildoutConfiguration.Build("./nonexistent-mcp.json");
        });

        Assert.NotNull(ex);
        Assert.Contains("Configuration file not found:", ex.Message);
        Assert.Contains("./nonexistent-mcp.json", ex.Message);
    }
}
