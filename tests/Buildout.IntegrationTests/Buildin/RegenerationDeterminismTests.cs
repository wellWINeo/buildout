using System.Diagnostics;
using Xunit;

namespace Buildout.IntegrationTests.Buildin;

public sealed class RegenerationDeterminismTests
{
    private static string RepoRoot =>
        FindRepoRoot(AppContext.BaseDirectory);

    private static string FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "buildout.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate repository root.");
    }

    [Fact(Skip = "Requires kiota tool restore. Run manually to verify regeneration determinism.")]
    public void RegenerateBuildinClient_ProducesCleanWorkingTree()
    {
        var scriptPath = Path.Combine(RepoRoot, "scripts", "regenerate-buildin-client.sh");
        Assert.True(File.Exists(scriptPath), $"Script not found: {scriptPath}");

        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"\"{scriptPath}\"",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start regeneration process.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0,
            $"Regeneration script failed (exit {process.ExitCode}).\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var generatedDir = Path.Combine(RepoRoot, "src", "Buildout.Core", "Buildin", "Generated");
        Assert.True(Directory.Exists(generatedDir), $"Generated directory missing: {generatedDir}");
        Assert.NotEmpty(Directory.GetFiles(generatedDir, "*.cs", SearchOption.AllDirectories));

        var gitPsi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "status --porcelain -- src/Buildout.Core/Buildin/Generated/",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var gitProcess = Process.Start(gitPsi)
            ?? throw new InvalidOperationException("Failed to start git process.");
        var gitOutput = gitProcess.StandardOutput.ReadToEnd();
        gitProcess.WaitForExit();

        Assert.True(string.IsNullOrWhiteSpace(gitOutput),
            $"Working tree is dirty after regeneration:\n{gitOutput}");
    }
}
