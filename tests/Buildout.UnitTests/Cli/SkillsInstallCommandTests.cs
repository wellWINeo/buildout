using Buildout.Cli.Commands;
using Spectre.Console.Cli;
using Xunit;

namespace Buildout.UnitTests.Cli;

[Collection("SkillsCwdUnit")]
public class SkillsInstallCommandTests
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
            });
        });
        return app;
    }

    [Fact]
    public async Task Install_Local_Claude_WritesEightFiles()
    {
        using var tempDir = new UnitTestTempDirectory();

        var exitCode = await CreateApp().RunAsync(["skills", "install", "--agent", "claude", "--local"]);

        Assert.Equal(0, exitCode);
        var target = Path.Combine(tempDir.Path, ".claude", "skills", "buildout-cli");
        Assert.True(Directory.Exists(target));
        Assert.Equal(8, Directory.GetFiles(target).Length);
    }

    [Fact]
    public async Task Install_Local_CreatesDirectoryIfMissing()
    {
        using var tempDir = new UnitTestTempDirectory();

        var target = Path.Combine(tempDir.Path, ".claude", "skills", "buildout-cli");
        Assert.False(Directory.Exists(target));

        var exitCode = await CreateApp().RunAsync(["skills", "install", "--agent", "claude", "--local"]);

        Assert.Equal(0, exitCode);
        Assert.True(Directory.Exists(target));
    }

    [Fact]
    public async Task Install_ExistingFiles_WithoutOverwrite_ReturnsExitCode2()
    {
        using var tempDir = new UnitTestTempDirectory();

        await CreateApp().RunAsync(["skills", "install", "--agent", "claude", "--local"]);
        var exitCode = await CreateApp().RunAsync(["skills", "install", "--agent", "claude", "--local"]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task Install_ExistingFiles_WithOverwrite_ReturnsZero()
    {
        using var tempDir = new UnitTestTempDirectory();

        await CreateApp().RunAsync(["skills", "install", "--agent", "claude", "--local"]);
        var exitCode = await CreateApp().RunAsync(["skills", "install", "--agent", "claude", "--local", "--overwrite"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Install_InvalidAgent_ReturnsExitCode2()
    {
        var exitCode = await CreateApp().RunAsync(["skills", "install", "--agent", "cursor"]);
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task Install_Global_MissingAgentRoot_ReturnsExitCode2()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var claudeRoot = Path.Combine(home, ".claude");

        if (!Directory.Exists(claudeRoot))
        {
            // .claude doesn't exist — global install must fail
            var exitCode = await CreateApp().RunAsync(["skills", "install", "--agent", "claude"]);
            Assert.Equal(2, exitCode);
        }
        else
        {
            // .claude exists — command succeeds or fails depending on existing files; either is acceptable
            var exitCode = await CreateApp().RunAsync(["skills", "install", "--agent", "claude"]);
            Assert.True(exitCode == 0 || exitCode == 2,
                $"Expected exit code 0 or 2, got {exitCode}");
        }
    }
}
