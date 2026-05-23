using Buildout.Cli.Commands;
using Spectre.Console.Cli;
using Xunit;

namespace Buildout.UnitTests.Cli;

[Collection("SkillsCwdUnit")]
public class SkillsRemoveCommandTests
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
    public async Task Remove_InstalledDirectory_DeletesItAndReturnsZero()
    {
        using var tempDir = new UnitTestTempDirectory();

        await CreateApp().RunAsync(["skills", "install", "--agent", "claude", "--local"]);
        var target = Path.Combine(tempDir.Path, ".claude", "skills", "buildout-cli");
        Assert.True(Directory.Exists(target));

        var exitCode = await CreateApp().RunAsync(["skills", "remove", "--agent", "claude", "--local"]);

        Assert.Equal(0, exitCode);
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public async Task Remove_NotInstalled_ReturnsZero()
    {
        using var tempDir = new UnitTestTempDirectory();

        var exitCode = await CreateApp().RunAsync(["skills", "remove", "--agent", "claude", "--local"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Remove_InvalidAgent_ReturnsExitCode2()
    {
        var exitCode = await CreateApp().RunAsync(["skills", "remove", "--agent", "unknown"]);
        Assert.Equal(2, exitCode);
    }
}
