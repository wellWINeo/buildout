using Buildout.Cli.Rendering;
using Spectre.Console.Testing;
using Xunit;

namespace Buildout.UnitTests.Cli;

public sealed class SearchResultStyledRendererTests
{
    private static (SearchResultStyledRenderer renderer, TestConsole console) CreateSut()
    {
        var console = new TestConsole();
        console.EmitAnsiSequences();
        var renderer = new SearchResultStyledRenderer(console);
        return (renderer, console);
    }

    [Fact]
    public void MultiLineBody_RendersTableWithThreeColumns()
    {
        var (sut, console) = CreateSut();
        var body = "id-1\tpage\tFirst Title\nid-2\tdatabase\tSecond Title\nid-3\tpage\tThird Title\n";
        sut.Render(body);
        var output = console.Output;
        Assert.Contains("ID", output);
        Assert.Contains("Type", output);
        Assert.Contains("Title", output);
    }

    [Fact]
    public void EachMatch_AppearsInCorrectColumns()
    {
        var (sut, console) = CreateSut();
        var body = "alpha\tpage\tAlpha Title\nbeta\tdatabase\tBeta Title\n";
        sut.Render(body);
        var output = console.Output;
        Assert.Contains("alpha", output);
        Assert.Contains("page", output);
        Assert.Contains("Alpha Title", output);
        Assert.Contains("beta", output);
        Assert.Contains("database", output);
        Assert.Contains("Beta Title", output);
    }

    [Fact]
    public void EmptyBody_RendersNoMatchesLine()
    {
        var (sut, console) = CreateSut();
        sut.Render("");
        var output = console.Output;
        Assert.Contains("No matches.", output);
        Assert.DoesNotContain("ID", output);
    }

    [Fact]
    public void MismatchedColumnCount_Throws()
    {
        var (sut, console) = CreateSut();
        var body = "only\ttwo\n";
        Assert.Throws<InvalidOperationException>(() => sut.Render(body));
    }
}
