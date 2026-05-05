using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Internal;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown.Blocks;

public class ToDoConverterTests
{
    private static (ToDoConverter converter, IMarkdownWriter writer, IMarkdownRenderContext ctx, IInlineRenderer inline)
        CreateSut(int indentLevel = 0)
    {
        var converter = new ToDoConverter();
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
    public void WritesExpectedMarkdownForUncheckedToDo()
    {
        var (converter, writer, ctx, inline) = CreateSut();
        var richText = new List<RichText> { new() { Type = "text", Content = "Buy milk" } };
        inline.Render(richText, 0).Returns("Buy milk");

        var block = new ToDoBlock { RichTextContent = richText, Checked = false };
        converter.Write(block, [], ctx);

        writer.Received().WriteLine("- [ ] Buy milk");
    }

    [Fact]
    public void WritesExpectedMarkdownForCheckedToDo()
    {
        var (converter, writer, ctx, inline) = CreateSut();
        var richText = new List<RichText> { new() { Type = "text", Content = "Done" } };
        inline.Render(richText, 0).Returns("Done");

        var block = new ToDoBlock { RichTextContent = richText, Checked = true };
        converter.Write(block, [], ctx);

        writer.Received().WriteLine("- [x] Done");
    }

    [Fact]
    public void WritesExpectedMarkdownForNullChecked()
    {
        var (converter, writer, ctx, inline) = CreateSut();
        var richText = new List<RichText> { new() { Type = "text", Content = "Task" } };
        inline.Render(richText, 0).Returns("Task");

        var block = new ToDoBlock { RichTextContent = richText, Checked = null };
        converter.Write(block, [], ctx);

        writer.Received().WriteLine("- [ ] Task");
    }

    [Fact]
    public void RecursesIntoChildrenWhenSupported()
    {
        var (converter, writer, ctx, inline) = CreateSut();
        var richText = new List<RichText> { new() { Type = "text", Content = "parent" } };
        inline.Render(richText, 0).Returns("parent");

        var childBlock = new ToDoBlock { RichTextContent = new List<RichText> { new() { Type = "text", Content = "child" } } };
        var children = new List<BlockSubtree>
        {
            new() { Block = childBlock, Children = [] }
        };

        var block = new ToDoBlock { RichTextContent = richText, Checked = false };
        converter.Write(block, children, ctx);

        ctx.Received().WriteBlockSubtree(children[0]);
    }

    [Fact]
    public void HonoursIndentLevelFromContext()
    {
        var (converter, writer, ctx, inline) = CreateSut(indentLevel: 2);
        var richText = new List<RichText> { new() { Type = "text", Content = "indented" } };
        inline.Render(richText, 2).Returns("indented");

        var block = new ToDoBlock { RichTextContent = richText, Checked = true };
        converter.Write(block, [], ctx);

        writer.Received().WriteLine("    - [x] indented");
    }

    [Fact]
    public void HandlesEmptyRichTextContent()
    {
        var (converter, writer, ctx, inline) = CreateSut();
        inline.Render(null, 0).Returns(string.Empty);

        var block = new ToDoBlock { RichTextContent = null, Checked = false };
        converter.Write(block, [], ctx);

        writer.Received().WriteLine("- [ ] ");
    }

    [Fact]
    public void BlockClrType_IsToDoBlock()
    {
        var converter = new ToDoConverter();
        Assert.Equal(typeof(ToDoBlock), converter.BlockClrType);
    }

    [Fact]
    public void BlockType_IsToDo()
    {
        var converter = new ToDoConverter();
        Assert.Equal("to_do", converter.BlockType);
    }

    [Fact]
    public void RecurseChildren_IsTrue()
    {
        var converter = new ToDoConverter();
        Assert.True(converter.RecurseChildren);
    }
}
