using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Internal;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown.Blocks;

public class QuoteConverterTests
{
    private readonly QuoteConverter _sut = new();

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
    public void BlockClrType_IsQuoteBlock()
    {
        Assert.Equal(typeof(QuoteBlock), _sut.BlockClrType);
    }

    [Fact]
    public void BlockType_IsQuote()
    {
        Assert.Equal("quote", _sut.BlockType);
    }

    [Fact]
    public void RecurseChildren_IsTrue()
    {
        Assert.True(_sut.RecurseChildren);
    }

    [Fact]
    public void Write_SingleLine_PrefixesWithBlockquote()
    {
        var (writer, inline, ctx) = CreateContext();
        inline.Render(default!, default).ReturnsForAnyArgs("hello world");
        var block = new QuoteBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "hello world" }]
        };

        _sut.Write(block, [], ctx);

        Received.InOrder(() =>
        {
            writer.WriteLine("> hello world");
            writer.WriteBlankLine();
        });
    }

    [Fact]
    public void Write_MultiLineContent_PrefixesEachLine()
    {
        var (writer, inline, ctx) = CreateContext();
        inline.Render(default!, default).ReturnsForAnyArgs("line1\nline2");
        var block = new QuoteBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "line1\nline2" }]
        };

        _sut.Write(block, [], ctx);

        Received.InOrder(() =>
        {
            writer.WriteLine("> line1");
            writer.WriteLine("> line2");
            writer.WriteBlankLine();
        });
    }

    [Fact]
    public void Write_NullRichTextContent_WritesEmptyQuote()
    {
        var (writer, inline, ctx) = CreateContext();
        inline.Render(null, 0).Returns(string.Empty);
        var block = new QuoteBlock { RichTextContent = null };

        _sut.Write(block, [], ctx);

        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_WithChildren_RecurseSubtrees()
    {
        var (writer, inline, ctx) = CreateContext();
        inline.Render(default!, default).ReturnsForAnyArgs("quoted text");
        var childSubtree = new BlockSubtree
        {
            Block = new ParagraphBlock
            {
                RichTextContent = [new RichText { Type = "text", Content = "child" }]
            }
        };
        var block = new QuoteBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "quoted text" }]
        };

        _sut.Write(block, [childSubtree], ctx);

        ctx.Received().WriteBlockSubtree(childSubtree);
    }

    [Fact]
    public void Write_UsesInlineRenderer()
    {
        var (writer, inline, ctx) = CreateContext();
        var richText = new[] { new RichText { Type = "text", Content = "hello" } };
        inline.Render(richText, Arg.Any<int>()).Returns("rendered");
        var block = new QuoteBlock { RichTextContent = richText };

        _sut.Write(block, [], ctx);

        inline.Received(1).Render(richText, Arg.Any<int>());
        writer.Received().WriteLine("> rendered");
    }
}
