using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Internal;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown.Blocks;

public class BulletedListItemConverterTests
{
    private static (BulletedListItemConverter converter, IMarkdownWriter writer, IMarkdownRenderContext ctx, IInlineRenderer inline)
        CreateSut(int indentLevel = 0)
    {
        var converter = new BulletedListItemConverter();
        var writer = Substitute.For<IMarkdownWriter>();
        var inline = Substitute.For<IInlineRenderer>();
        var ctx = Substitute.For<IMarkdownRenderContext>();
        ctx.Writer.Returns(writer);
        ctx.Inline.Returns(inline);
        ctx.IndentLevel.Returns(indentLevel);
        ctx.WithIndent(Arg.Any<int>()).Returns(ctx);
        return (converter, writer, ctx, inline);
    }

    [Fact]
    public void WritesExpectedMarkdownForCanonicalBlock()
    {
        var (converter, writer, ctx, inline) = CreateSut();
        var richText = new List<RichText> { new() { Type = "text", Content = "Hello" } };
        inline.Render(richText, 0).Returns("Hello");

        var block = new BulletedListItemBlock { RichTextContent = richText };
        converter.Write(block, [], ctx);

        writer.Received().WriteLine("- Hello");
    }

    [Fact]
    public void RecursesIntoChildrenWhenSupported()
    {
        var (converter, writer, ctx, inline) = CreateSut();
        var richText = new List<RichText> { new() { Type = "text", Content = "parent" } };
        inline.Render(richText, 0).Returns("parent");

        var childBlock = new BulletedListItemBlock { RichTextContent = new List<RichText> { new() { Type = "text", Content = "child" } } };
        var children = new List<BlockSubtree>
        {
            new() { Block = childBlock, Children = [] }
        };

        var block = new BulletedListItemBlock { RichTextContent = richText };
        converter.Write(block, children, ctx);

        ctx.Received().WriteBlockSubtree(children[0]);
    }

    [Fact]
    public void HonoursIndentLevelFromContext()
    {
        var (converter, writer, ctx, inline) = CreateSut(indentLevel: 2);
        var richText = new List<RichText> { new() { Type = "text", Content = "indented" } };
        inline.Render(richText, 2).Returns("indented");

        var block = new BulletedListItemBlock { RichTextContent = richText };
        converter.Write(block, [], ctx);

        writer.Received().WriteLine("    - indented");
    }

    [Fact]
    public void HandlesEmptyRichTextContent()
    {
        var (converter, writer, ctx, inline) = CreateSut();
        inline.Render(null, 0).Returns(string.Empty);

        var block = new BulletedListItemBlock { RichTextContent = null };
        converter.Write(block, [], ctx);

        writer.Received().WriteLine("- ");
    }

    [Fact]
    public void BlockClrType_IsBulletedListItemBlock()
    {
        var converter = new BulletedListItemConverter();
        Assert.Equal(typeof(BulletedListItemBlock), converter.BlockClrType);
    }

    [Fact]
    public void BlockType_IsBulletedListItem()
    {
        var converter = new BulletedListItemConverter();
        Assert.Equal("bulleted_list_item", converter.BlockType);
    }

    [Fact]
    public void RecurseChildren_IsTrue()
    {
        var converter = new BulletedListItemConverter();
        Assert.True(converter.RecurseChildren);
    }
}
