using System.Text.RegularExpressions;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown;

public class UnsupportedBlockHandlerTests
{
    private static (IMarkdownWriter writer, IMarkdownRenderContext ctx) CreateContext()
    {
        var writer = Substitute.For<IMarkdownWriter>();
        var ctx = Substitute.For<IMarkdownRenderContext>();
        ctx.Writer.Returns(writer);
        return (writer, ctx);
    }

    [Fact]
    public void Write_ToggleBlock_WritesCorrectPlaceholder()
    {
        var (writer, ctx) = CreateContext();
        UnsupportedBlockHandler.Write(new ToggleBlock(), ctx);
        writer.Received().WriteLine("<!-- unsupported block: toggle -->");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_ImageBlock_WritesCorrectPlaceholder()
    {
        var (writer, ctx) = CreateContext();
        UnsupportedBlockHandler.Write(new ImageBlock(), ctx);
        writer.Received().WriteLine("<!-- unsupported block: image -->");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_EmbedBlock_WritesCorrectPlaceholder()
    {
        var (writer, ctx) = CreateContext();
        UnsupportedBlockHandler.Write(new EmbedBlock(), ctx);
        writer.Received().WriteLine("<!-- unsupported block: embed -->");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_TableBlock_WritesCorrectPlaceholder()
    {
        var (writer, ctx) = CreateContext();
        UnsupportedBlockHandler.Write(new TableBlock(), ctx);
        writer.Received().WriteLine("<!-- unsupported block: table -->");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_TableRowBlock_WritesCorrectPlaceholder()
    {
        var (writer, ctx) = CreateContext();
        UnsupportedBlockHandler.Write(new TableRowBlock(), ctx);
        writer.Received().WriteLine("<!-- unsupported block: table_row -->");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_ColumnListBlock_WritesCorrectPlaceholder()
    {
        var (writer, ctx) = CreateContext();
        UnsupportedBlockHandler.Write(new ColumnListBlock(), ctx);
        writer.Received().WriteLine("<!-- unsupported block: column_list -->");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_ColumnBlock_WritesCorrectPlaceholder()
    {
        var (writer, ctx) = CreateContext();
        UnsupportedBlockHandler.Write(new ColumnBlock(), ctx);
        writer.Received().WriteLine("<!-- unsupported block: column -->");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_ChildPageBlock_WritesCorrectPlaceholder()
    {
        var (writer, ctx) = CreateContext();
        UnsupportedBlockHandler.Write(new ChildPageBlock(), ctx);
        writer.Received().WriteLine("<!-- unsupported block: child_page -->");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_ChildDatabaseBlock_WritesCorrectPlaceholder()
    {
        var (writer, ctx) = CreateContext();
        UnsupportedBlockHandler.Write(new ChildDatabaseBlock(), ctx);
        writer.Received().WriteLine("<!-- unsupported block: child_database -->");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_SyncedBlock_WritesCorrectPlaceholder()
    {
        var (writer, ctx) = CreateContext();
        UnsupportedBlockHandler.Write(new SyncedBlock(), ctx);
        writer.Received().WriteLine("<!-- unsupported block: synced_block -->");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_LinkPreviewBlock_WritesCorrectPlaceholder()
    {
        var (writer, ctx) = CreateContext();
        UnsupportedBlockHandler.Write(new LinkPreviewBlock(), ctx);
        writer.Received().WriteLine("<!-- unsupported block: link_preview -->");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_UnsupportedBlock_WritesCorrectPlaceholder()
    {
        var (writer, ctx) = CreateContext();
        UnsupportedBlockHandler.Write(new UnsupportedBlock(), ctx);
        writer.Received().WriteLine("<!-- unsupported block: unsupported -->");
        writer.Received().WriteBlankLine();
    }

    [Fact]
    public void Write_PlaceholderIsValidHtmlComment()
    {
        var (writer, ctx) = CreateContext();
        UnsupportedBlockHandler.Write(new ToggleBlock(), ctx);

        var calls = writer.ReceivedCalls().ToList();
        var writeLineCall = calls.First(c =>
            c.GetMethodInfo().Name == nameof(IMarkdownWriter.WriteLine));
        var arg = (string)writeLineCall.GetArguments()[0]!;

        Assert.Matches(@"^<!-- unsupported block: .+ -->$", arg);
    }
}
