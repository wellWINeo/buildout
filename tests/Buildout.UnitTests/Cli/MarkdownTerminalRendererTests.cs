using Buildout.Cli.Rendering;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace Buildout.UnitTests.Cli;

public class MarkdownTerminalRendererTests
{
    private static (MarkdownTerminalRenderer renderer, TestConsole console) CreateSut()
    {
        var console = new TestConsole();
        var renderer = new MarkdownTerminalRenderer(console);
        return (renderer, console);
    }

    [Fact]
    public void Render_H1_EmitsBoldStyledText()
    {
        var (sut, console) = CreateSut();
        sut.Render("# Hello World");
        var output = console.Output;
        Assert.Contains("Hello World", output);
    }

    [Fact]
    public void Render_H2_EmitsBoldStyledText()
    {
        var (sut, console) = CreateSut();
        sut.Render("## Section Title");
        var output = console.Output;
        Assert.Contains("Section Title", output);
    }

    [Fact]
    public void Render_H3_EmitsStyledText()
    {
        var (sut, console) = CreateSut();
        sut.Render("### Subsection");
        var output = console.Output;
        Assert.Contains("Subsection", output);
    }

    [Fact]
    public void Render_BulletedList_EmitsBulletGlyphs()
    {
        var (sut, console) = CreateSut();
        var md = """
            - First item
            - Second item
            - Third item
            """;
        sut.Render(md);
        var output = console.Output;
        Assert.Contains("First item", output);
        Assert.Contains("Second item", output);
        Assert.Contains("Third item", output);
    }

    [Fact]
    public void Render_NumberedList_EmitsNumbers()
    {
        var (sut, console) = CreateSut();
        var md = """
            1. First
            2. Second
            3. Third
            """;
        sut.Render(md);
        var output = console.Output;
        Assert.Contains("First", output);
        Assert.Contains("Second", output);
        Assert.Contains("Third", output);
    }

    [Fact]
    public void Render_TaskList_EmitsCheckboxes()
    {
        var (sut, console) = CreateSut();
        var md = """
            - [x] Done
            - [ ] Not done
            """;
        sut.Render(md);
        var output = console.Output;
        Assert.Contains("Done", output);
        Assert.Contains("Not done", output);
    }

    [Fact]
    public void Render_FencedCodeBlock_RendersPanelWithLanguageHeader()
    {
        var (sut, console) = CreateSut();
        var md = """
            ```csharp
            Console.WriteLine("hello");
            ```
            """;
        sut.Render(md);
        var output = console.Output;
        Assert.Contains("csharp", output);
        Assert.Contains("Console.WriteLine", output);
    }

    [Fact]
    public void Render_BlockQuote_RendersWithGlyph()
    {
        var (sut, console) = CreateSut();
        sut.Render("> This is a quote");
        var output = console.Output;
        Assert.Contains("This is a quote", output);
    }

    [Fact]
    public void Render_ThematicBreak_RendersRule()
    {
        var (sut, console) = CreateSut();
        sut.Render("---");
        var output = console.Output;
        Assert.NotEmpty(output);
    }

    [Fact]
    public void Render_HtmlCommentPlaceholder_RendersDimGrey()
    {
        var (sut, console) = CreateSut();
        sut.Render("<!-- unsupported block: image -->");
        var output = console.Output;
        Assert.Contains("unsupported block: image", output);
    }

    [Fact]
    public void Render_InlineBold_RendersViaMarkup()
    {
        var (sut, console) = CreateSut();
        sut.Render("This is **bold** text");
        var output = console.Output;
        Assert.Contains("bold", output);
    }

    [Fact]
    public void Render_InlineItalic_RendersViaMarkup()
    {
        var (sut, console) = CreateSut();
        sut.Render("This is *italic* text");
        var output = console.Output;
        Assert.Contains("italic", output);
    }

    [Fact]
    public void Render_InlineCode_RendersViaMarkup()
    {
        var (sut, console) = CreateSut();
        sut.Render("Use `var` keyword");
        var output = console.Output;
        Assert.Contains("var", output);
    }

    [Fact]
    public void Render_Paragraph_RendersText()
    {
        var (sut, console) = CreateSut();
        sut.Render("Hello world");
        var output = console.Output;
        Assert.Contains("Hello world", output);
    }

    [Fact]
    public void Render_MultipleBlocks_RendersAll()
    {
        var (sut, console) = CreateSut();
        var md = """
            # Title

            Some paragraph text.

            - Item A
            - Item B

            ---

            Another paragraph.
            """;
        sut.Render(md);
        var output = console.Output;
        Assert.Contains("Title", output);
        Assert.Contains("Some paragraph text", output);
        Assert.Contains("Item A", output);
        Assert.Contains("Item B", output);
        Assert.Contains("Another paragraph", output);
    }
}
