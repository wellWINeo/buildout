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
            var cliDllPath = GetBuiltDllPath("Buildout.Cli");
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "exec \"" + cliDllPath + "\" --help",
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

            Assert.True(process.WaitForExit(10000), "CLI process did not exit in time");

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
            var mcpDllPath = GetBuiltDllPath("Buildout.Mcp");
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "exec \"" + mcpDllPath + "\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
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

            // Give the MCP server a moment to emit any startup output, then close stdin to signal EOF
            Thread.Sleep(1000);
            process.StandardInput.Close();

            // Process should exit shortly after stdin is closed (stdio transport sees EOF)
            _ = process.WaitForExit(5000);
            if (!process.HasExited)
                process.Kill();

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
            var mcpDllPath = GetBuiltDllPath("Buildout.Mcp");
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "exec \"" + mcpDllPath + "\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
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

            // Give the MCP server a moment to emit any startup output, then close stdin to signal EOF
            await Task.Delay(1000);
            process.StandardInput.Close();

            // Process should exit shortly after stdin is closed (stdio transport sees EOF)
            _ = process.WaitForExit(5000);
            if (!process.HasExited)
                process.Kill();

            var allOutput = outputBuilder.ToString() + errorBuilder.ToString();

            Assert.DoesNotContain(secretToken, allOutput);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Buildout__BotToken", null);
        }
    }

    private static string GetBuiltDllPath(string projectName)
    {
        // Derive the configuration (Release/Debug) and TFM from where THIS test assembly lives.
        // AppContext.BaseDirectory ends in:  …/tests/Buildout.IntegrationTests/bin/{Config}/{TFM}/
        // The target project DLL is at:      …/src/{projectName}/bin/{Config}/{TFM}/{projectName}.dll
        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, '/');
        var tfm = Path.GetFileName(baseDir);
        var configuration = Path.GetFileName(Path.GetDirectoryName(baseDir)!);

        var solutionDir = FindSolutionRoot();
        var dllPath = Path.Combine(solutionDir, "src", projectName, "bin", configuration, tfm, $"{projectName}.dll");

        Assert.True(File.Exists(dllPath),
            $"Pre-built DLL not found at {dllPath}. " +
            $"Run 'dotnet build -c {configuration}' before running these tests.");

        return dllPath;
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate repository root (no .git directory found).");
    }
}
