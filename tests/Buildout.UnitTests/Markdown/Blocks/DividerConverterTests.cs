using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Internal;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown.Blocks;

public class DividerConverterTests
{
    private readonly DividerConverter _sut = new();

    private static (IMarkdownWriter writer, IMarkdownRenderContext ctx) CreateContext()
    {
        var writer = Substitute.For<IMarkdownWriter>();
        var ctx = Substitute.For<IMarkdownRenderContext>();
        ctx.Writer.Returns(writer);
        return (writer, ctx);
    }

    [Fact]
    public void BlockClrType_IsDividerBlock()
    {
        Assert.Equal(typeof(DividerBlock), _sut.BlockClrType);
    }

    [Fact]
    public void BlockType_IsDivider()
    {
        Assert.Equal("divider", _sut.BlockType);
    }

    [Fact]
    public void RecurseChildren_IsFalse()
    {
        Assert.False(_sut.RecurseChildren);
    }

    [Fact]
    public void Write_WritesHorizontalRuleAndBlankLine()
    {
        var (writer, ctx) = CreateContext();
        var block = new DividerBlock();

        _sut.Write(block, [], ctx);

        Received.InOrder(() =>
        {
            writer.WriteLine("---");
            writer.WriteBlankLine();
        });
    }

    [Fact]
    public void Write_IgnoresChildren()
    {
        var (_, ctx) = CreateContext();
        var children = new[]
        {
            new BlockSubtree
            {
                Block = new ParagraphBlock
                {
                    RichTextContent = [new RichText { Type = "text", Content = "ignored" }]
                }
            }
        };

        _sut.Write(new DividerBlock(), children, ctx);

        ctx.DidNotReceive().WriteBlockSubtree(Arg.Any<BlockSubtree>());
    }
}
