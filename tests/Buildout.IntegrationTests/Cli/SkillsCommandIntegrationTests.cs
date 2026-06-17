using Buildout.Cli.Commands;
using Buildout.IntegrationTests.Buildin;
using Spectre.Console.Cli;
using Xunit;

namespace Buildout.IntegrationTests.Cli;

[Collection("SkillsCwd")]
public class SkillsCommandIntegrationTests
{
    private static CommandApp CreateApp()
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.PropagateExceptions();
            config.AddBranch<SkillsSettings>("skills", skills =>
            {
                skills.AddCommand<SkillsInstallCommand>("install");
                skills.AddCommand<SkillsRemoveCommand>("remove");
            });
        });
        return app;
    }

    [Fact]
    public async Task Install_Claude_Local_WritesNineSkillFiles()
    {
        using var tempDir = FileSystemFixture.CreateTempDirectory();
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir.Path);
        try
        {
            var exitCode = await CreateApp().RunAsync(["skills", "install", "--agent", "claude", "--local"]);

            Assert.Equal(0, exitCode);
            var skillsDir = Path.Combine(tempDir.Path, ".claude", "skills", "buildout-cli");
            Assert.True(Directory.Exists(skillsDir), $"Expected directory: {skillsDir}");
            Assert.Equal(9, Directory.GetFiles(skillsDir).Length);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Install_Opencode_Local_CreatesDirectoryAndWritesFiles()
    {
        using var tempDir = FileSystemFixture.CreateTempDirectory();
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir.Path);
        try
        {
            var exitCode = await CreateApp().RunAsync(["skills", "install", "--agent", "opencode", "--local"]);

            Assert.Equal(0, exitCode);
            var skillsDir = Path.Combine(tempDir.Path, ".opencode", "skills", "buildout-cli");
            Assert.True(Directory.Exists(skillsDir));
            Assert.Equal(9, Directory.GetFiles(skillsDir).Length);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Install_ExistingFiles_WithoutOverwrite_ReturnsExitCode2()
    {
        using var tempDir = FileSystemFixture.CreateTempDirectory();
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir.Path);
        try
        {
            await CreateApp().RunAsync(["skills", "install", "--agent", "claude", "--local"]);
            var exitCode = await CreateApp().RunAsync(["skills", "install", "--agent", "claude", "--local"]);

            Assert.Equal(2, exitCode);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Install_ExistingFiles_WithOverwrite_Succeeds()
    {
        using var tempDir = FileSystemFixture.CreateTempDirectory();
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir.Path);
        try
        {
            await CreateApp().RunAsync(["skills", "install", "--agent", "claude", "--local"]);
            var exitCode = await CreateApp().RunAsync(["skills", "install", "--agent", "claude", "--local", "--overwrite"]);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Install_CompletesUnderOneSecond()
    {
        using var tempDir = FileSystemFixture.CreateTempDirectory();
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir.Path);
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await CreateApp().RunAsync(["skills", "install", "--agent", "claude", "--local"]);
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 1000, $"Install took {sw.ElapsedMilliseconds}ms");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Install_InvalidAgent_ReturnsExitCode2()
    {
        var exitCode = await CreateApp().RunAsync(["skills", "install", "--agent", "cursor"]);
        Assert.Equal(2, exitCode);
    }
}
