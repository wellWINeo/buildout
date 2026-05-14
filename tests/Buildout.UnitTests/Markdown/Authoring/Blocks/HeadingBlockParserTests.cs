using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Blocks;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;
using NSubstitute;
using Xunit;
using Md = Markdig.Markdown;
using ParaBlock = Buildout.Core.Buildin.Models.ParagraphBlock;

namespace Buildout.UnitTests.Markdown.Authoring.Blocks;

public class HeadingBlockParserTests
{
    private readonly HeadingBlockParser _sut = new();

    private static Markdig.Syntax.HeadingBlock ParseHeading(string markdown)
    {
        var doc = Md.Parse(markdown);
        return doc.OfType<Markdig.Syntax.HeadingBlock>().First();
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
    public void H1_ReturnsHeading1Block()
    {
        var block = ParseHeading("# Title");
        var result = _sut.Parse(block, CreateInlineParser());
        Assert.IsType<Heading1Block>(result.Block);
    }

    [Fact]
    public void H2_ReturnsHeading2Block()
    {
        var block = ParseHeading("## Section");
        var result = _sut.Parse(block, CreateInlineParser());
        Assert.IsType<Heading2Block>(result.Block);
    }

    [Fact]
    public void H3_ReturnsHeading3Block()
    {
        var block = ParseHeading("### Subsection");
        var result = _sut.Parse(block, CreateInlineParser());
        Assert.IsType<Heading3Block>(result.Block);
    }

    [Fact]
    public void H4_FallsThroughToParagraphBlock()
    {
        var block = ParseHeading("#### Deep heading");
        var result = _sut.Parse(block, CreateInlineParser());
        Assert.IsType<ParaBlock>(result.Block);
        var para = (ParaBlock)result.Block;
        Assert.Contains("####", para.RichTextContent![0].Content);
    }

    [Fact]
    public void CanParse_HeadingBlock_ReturnsTrue()
    {
        var block = ParseHeading("# test");
        Assert.True(_sut.CanParse(block));
    }
}
