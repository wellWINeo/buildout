using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Internal;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown.Blocks;

public class CodeConverterTests
{
    private readonly CodeConverter _sut = new();

    private static (IMarkdownWriter writer, IInlineRenderer inline, IMarkdownRenderContext ctx) CreateContext()
    {
        var writer = Substitute.For<IMarkdownWriter>();
        var inline = Substitute.For<IInlineRenderer>();
        var ctx = Substitute.For<IMarkdownRenderContext>();
        ctx.Writer.Returns(writer);
        ctx.Inline.Returns(inline);
        return (writer, inline, ctx);
    }

    [Fact]
    public void BlockClrType_IsCodeBlock()
    {
        Assert.Equal(typeof(CodeBlock), _sut.BlockClrType);
    }

    [Fact]
    public void BlockType_IsCode()
    {
        Assert.Equal("code", _sut.BlockType);
    }

    [Fact]
    public void RecurseChildren_IsFalse()
    {
        Assert.False(_sut.RecurseChildren);
    }

    [Fact]
    public void Write_WithLanguage_WritesFencedCodeBlock()
    {
        var (writer, _, ctx) = CreateContext();
        var block = new CodeBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "print('hello')" }],
            Language = "python"
        };

        _sut.Write(block, [], ctx);

        Received.InOrder(() =>
        {
            writer.WriteLine("```python");
            writer.WriteLine("print('hello')");
            writer.WriteLine("```");
            writer.WriteBlankLine();
        });
    }

    [Fact]
    public void Write_WithoutLanguage_WritesFencedCodeBlock()
    {
        var (writer, _, ctx) = CreateContext();
        var block = new CodeBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "let x = 1" }],
            Language = null
        };

        _sut.Write(block, [], ctx);

        Received.InOrder(() =>
        {
            writer.WriteLine("```");
            writer.WriteLine("let x = 1");
            writer.WriteLine("```");
            writer.WriteBlankLine();
        });
    }

    [Fact]
    public void Write_EmptyLanguage_WritesFencedCodeBlockWithoutLang()
    {
        var (writer, _, ctx) = CreateContext();
        var block = new CodeBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "code" }],
            Language = ""
        };

        _sut.Write(block, [], ctx);

        writer.Received().WriteLine("```");
    }

    [Fact]
    public void Write_MultiLineContent_PreservesNewlines()
    {
        var (writer, _, ctx) = CreateContext();
        var block = new CodeBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "line1\nline2\nline3" }],
            Language = "csharp"
        };

        _sut.Write(block, [], ctx);

        Received.InOrder(() =>
        {
            writer.WriteLine("```csharp");
            writer.WriteLine("line1\nline2\nline3");
            writer.WriteLine("```");
            writer.WriteBlankLine();
        });
    }

    [Fact]
    public void Write_MultipleRichTextItems_ConcatenatesContent()
    {
        var (writer, _, ctx) = CreateContext();
        var block = new CodeBlock
        {
            RichTextContent =
            [
                new RichText { Type = "text", Content = "foo" },
                new RichText { Type = "text", Content = "bar" }
            ],
            Language = null
        };

        _sut.Write(block, [], ctx);

        writer.Received().WriteLine("foobar");
    }

    [Fact]
    public void Write_NullRichTextContent_WritesEmptyCodeBlock()
    {
        var (writer, _, ctx) = CreateContext();
        var block = new CodeBlock { RichTextContent = null, Language = null };

        _sut.Write(block, [], ctx);

        Received.InOrder(() =>
        {
            writer.WriteLine("```");
            writer.WriteLine("");
            writer.WriteLine("```");
            writer.WriteBlankLine();
        });
    }
}
