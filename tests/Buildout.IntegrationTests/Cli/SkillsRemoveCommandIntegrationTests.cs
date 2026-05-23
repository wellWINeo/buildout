using Buildout.Cli.Commands;
using Buildout.IntegrationTests.Buildin;
using Spectre.Console.Cli;
using Xunit;

namespace Buildout.IntegrationTests.Cli;

[Collection("SkillsCwd")]
public class SkillsRemoveCommandIntegrationTests
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
    public async Task Remove_AfterInstall_DeletesDirectory()
    {
        using var tempDir = FileSystemFixture.CreateTempDirectory();
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir.Path);
        try
        {
            await CreateApp().RunAsync(["skills", "install", "--agent", "claude", "--local"]);

            var skillsDir = Path.Combine(tempDir.Path, ".claude", "skills", "buildout-cli");
            Assert.True(Directory.Exists(skillsDir));

            var exitCode = await CreateApp().RunAsync(["skills", "remove", "--agent", "claude", "--local"]);

            Assert.Equal(0, exitCode);
            Assert.False(Directory.Exists(skillsDir));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Remove_WhenNotInstalled_ReturnsZero()
    {
        using var tempDir = FileSystemFixture.CreateTempDirectory();
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir.Path);
        try
        {
            var exitCode = await CreateApp().RunAsync(["skills", "remove", "--agent", "claude", "--local"]);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Remove_Opencode_Local_Works()
    {
        using var tempDir = FileSystemFixture.CreateTempDirectory();
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir.Path);
        try
        {
            await CreateApp().RunAsync(["skills", "install", "--agent", "opencode", "--local"]);
            var exitCode = await CreateApp().RunAsync(["skills", "remove", "--agent", "opencode", "--local"]);

            Assert.Equal(0, exitCode);
            var skillsDir = Path.Combine(tempDir.Path, ".opencode", "skills", "buildout-cli");
            Assert.False(Directory.Exists(skillsDir));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Remove_InvalidAgent_ReturnsExitCode2()
    {
        var exitCode = await CreateApp().RunAsync(["skills", "remove", "--agent", "cursor"]);
        Assert.Equal(2, exitCode);
    }
}
