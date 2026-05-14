using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Blocks;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;
using NSubstitute;
using Xunit;
using Md = Markdig.Markdown;
using ParaBlock = Buildout.Core.Buildin.Models.ParagraphBlock;

namespace Buildout.UnitTests.Markdown.Authoring.Blocks;

public class ParagraphBlockParserTests
{
    private readonly ParagraphBlockParser _sut = new();

    private static Markdig.Syntax.ParagraphBlock ParseParagraph(string markdown)
    {
        var doc = Md.Parse(markdown);
        return doc.OfType<Markdig.Syntax.ParagraphBlock>().First();
    }

    private static IInlineMarkdownParser CreateInlineParser()
    {
        var parser = Substitute.For<IInlineMarkdownParser>();
        parser.ParseInlines(Arg.Any<Markdig.Syntax.Inlines.ContainerInline>())
            .Returns(call =>
            {
                var container = call.Arg<Markdig.Syntax.Inlines.ContainerInline>();
                var text = string.Join("", container.OfType<Markdig.Syntax.Inlines.LiteralInline>().Select(l => l.Content.ToString()));
                return new List<RichText> { new() { Type = "text", Content = text } };
            });
        return parser;
    }

    [Fact]
    public void CanParse_ParagraphBlock_ReturnsTrue()
    {
        var block = ParseParagraph("hello");
        Assert.True(_sut.CanParse(block));
    }

    [Fact]
    public void CanParse_HeadingBlock_ReturnsFalse()
    {
        var doc = Md.Parse("# heading");
        var heading = doc.OfType<Markdig.Syntax.HeadingBlock>().First();
        Assert.False(_sut.CanParse(heading));
    }

    [Fact]
    public void Parse_PlainText_ReturnsParagraphBlock()
    {
        var block = ParseParagraph("hello world");
        var result = _sut.Parse(block, CreateInlineParser());
        Assert.IsType<ParaBlock>(result.Block);
        Assert.Equal("paragraph", result.Block.Type);
        Assert.Empty(result.Children);
    }

    [Fact]
    public void Parse_PreservesRichTextContent()
    {
        var block = ParseParagraph("hello world");
        var result = _sut.Parse(block, CreateInlineParser());
        var para = (ParaBlock)result.Block;
        Assert.NotNull(para.RichTextContent);
        Assert.NotEmpty(para.RichTextContent);
    }
}
