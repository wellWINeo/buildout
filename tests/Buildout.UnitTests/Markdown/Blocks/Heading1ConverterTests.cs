using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown.Blocks;

public class Heading1ConverterTests
{
    private static (Heading1Converter sut, IMarkdownWriter writer, IMarkdownRenderContext ctx) CreateSut()
    {
        var writer = Substitute.For<IMarkdownWriter>();
        var inline = Substitute.For<IInlineRenderer>();
        inline.Render(Arg.Any<IReadOnlyList<RichText>?>(), Arg.Any<int>())
            .Returns(call =>
            {
                var items = call.Arg<IReadOnlyList<RichText>?>();
                return items is null ? "" : string.Join("", items.Select(r => r.Content));
            });

        var ctx = Substitute.For<IMarkdownRenderContext>();
        ctx.Writer.Returns(writer);
        ctx.Inline.Returns(inline);

        return (new Heading1Converter(), writer, ctx);
    }

    [Fact]
    public void BlockClrType_ReturnsHeading1Block()
    {
        var (sut, _, _) = CreateSut();
        Assert.Equal(typeof(Heading1Block), sut.BlockClrType);
    }

    [Fact]
    public void BlockType_ReturnsHeading1()
    {
        var (sut, _, _) = CreateSut();
        Assert.Equal("heading_1", sut.BlockType);
    }

    [Fact]
    public void RecurseChildren_ReturnsFalse()
    {
        var (sut, _, _) = CreateSut();
        Assert.False(sut.RecurseChildren);
    }

    [Fact]
    public void WritesExpectedMarkdownForCanonicalBlock()
    {
        var (sut, writer, ctx) = CreateSut();
        var block = new Heading1Block
        {
            RichTextContent = [new() { Type = "text", Content = "Introduction" }]
        };

        sut.Write(block, [], ctx);

        writer.Received().WriteLine("## Introduction");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void HandlesEmptyRichTextContent()
    {
        var (sut, writer, ctx) = CreateSut();
        var block = new Heading1Block { RichTextContent = null };

        sut.Write(block, [], ctx);

        writer.Received().WriteLine("## ");
        writer.Received().WriteBlankLine();
    }
}
