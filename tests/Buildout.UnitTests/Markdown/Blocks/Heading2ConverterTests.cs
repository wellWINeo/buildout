using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown.Blocks;

public class Heading2ConverterTests
{
    private static (Heading2Converter sut, IMarkdownWriter writer, IMarkdownRenderContext ctx) CreateSut()
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

        return (new Heading2Converter(), writer, ctx);
    }

    [Fact]
    public void BlockClrType_ReturnsHeading2Block()
    {
        var (sut, _, _) = CreateSut();
        Assert.Equal(typeof(Heading2Block), sut.BlockClrType);
    }

    [Fact]
    public void BlockType_ReturnsHeading2()
    {
        var (sut, _, _) = CreateSut();
        Assert.Equal("heading_2", sut.BlockType);
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
        var block = new Heading2Block
        {
            RichTextContent = [new() { Type = "text", Content = "Section Title" }]
        };

        sut.Write(block, [], ctx);

        writer.Received().WriteLine("### Section Title");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void HandlesEmptyRichTextContent()
    {
        var (sut, writer, ctx) = CreateSut();
        var block = new Heading2Block { RichTextContent = null };

        sut.Write(block, [], ctx);

        writer.Received().WriteLine("### ");
        writer.Received().WriteBlankLine();
    }
}
