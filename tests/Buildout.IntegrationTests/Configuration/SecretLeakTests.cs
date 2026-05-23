using System.Diagnostics;
using System.Text;
using Xunit;

namespace Buildout.IntegrationTests.Configuration;

public sealed class SecretLeakTests
{
    [Fact]
    public void BotToken_DoesNotLeakInCliStartup_Output()
    {
        var secretToken = "DO_NOT_LEAK_BUILDOUT_TOKEN_123";
        Environment.SetEnvironmentVariable("Buildout__BotToken", secretToken);

        try
        {
            var cliProjectPath = GetProjectPath("Buildout.Cli");
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project \"" + cliProjectPath + "\" -- --help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Assert.True(process.WaitForExit(30000), "CLI process did not exit in time");

            var allOutput = outputBuilder.ToString() + errorBuilder.ToString();

            Assert.DoesNotContain(secretToken, allOutput);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Buildout__BotToken", null);
        }
    }

    [Fact]
    public void BotToken_DoesNotLeakInMcpStartup_Output()
    {
        var secretToken = "DO_NOT_LEAK_BUILDOUT_TOKEN_123";
        Environment.SetEnvironmentVariable("Buildout__BotToken", secretToken);

        try
        {
            var mcpProjectPath = GetProjectPath("Buildout.Mcp");
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project \"" + mcpProjectPath + "\" --help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Assert.True(process.WaitForExit(30000), "MCP process did not exit in time");

            var allOutput = outputBuilder.ToString() + errorBuilder.ToString();

            Assert.DoesNotContain(secretToken, allOutput);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Buildout__BotToken", null);
        }
    }

    [Fact]
    public async Task BotToken_DoesNotLeakInMcpStartupShutdownCycle()
    {
        var secretToken = "DO_NOT_LEAK_BUILDOUT_TOKEN_123";
        Environment.SetEnvironmentVariable("Buildout__BotToken", secretToken);

        try
        {
            var mcpProjectPath = GetProjectPath("Buildout.Mcp");
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project \"" + mcpProjectPath + "\" --help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Assert.True(process.WaitForExit(30000), "MCP process did not exit in time");

            var allOutput = outputBuilder.ToString() + errorBuilder.ToString();

            Assert.DoesNotContain(secretToken, allOutput);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Buildout__BotToken", null);
        }
    }

    private static string GetProjectPath(string projectName)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var solutionDir = currentDir;

        while (solutionDir != null && !Directory.Exists(Path.Combine(solutionDir, ".git")))
        {
            solutionDir = Directory.GetParent(solutionDir)?.FullName;
        }

        Assert.NotNull(solutionDir);
        Assert.NotEmpty(solutionDir);

        var projectPath = Path.Combine(solutionDir ?? "", "src", projectName);
        Assert.True(Directory.Exists(projectPath), $"Project path does not exist: {projectPath}");

        return Path.Combine(projectPath, projectName + ".csproj");
    }
}