using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Blocks;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;
using NSubstitute;
using Xunit;
using Md = Markdig.Markdown;

namespace Buildout.UnitTests.Markdown.Authoring.Blocks;

public class DividerBlockParserTests
{
    private readonly DividerBlockParser _sut = new();
    private readonly IInlineMarkdownParser _inlineParser = Substitute.For<IInlineMarkdownParser>();

    [Fact]
    public void CanParse_ThematicBreak_ReturnsTrue()
    {
        var doc = Md.Parse("---");
        var divider = doc.OfType<ThematicBreakBlock>().First();
        Assert.True(_sut.CanParse(divider));
    }

    [Fact]
    public void CanParse_ParagraphBlock_ReturnsFalse()
    {
        var doc = Md.Parse("hello");
        var para = doc.OfType<Markdig.Syntax.ParagraphBlock>().First();
        Assert.False(_sut.CanParse(para));
    }

    [Fact]
    public void Parse_Dashes_ReturnsDividerBlock()
    {
        var doc = Md.Parse("---");
        var divider = doc.OfType<ThematicBreakBlock>().First();
        var result = _sut.Parse(divider, _inlineParser);
        var block = Assert.IsType<DividerBlock>(result.Block);
        Assert.Equal("divider", block.Type);
    }

    [Fact]
    public void Parse_Asterisks_ReturnsDividerBlock()
    {
        var doc = Md.Parse("***");
        var divider = doc.OfType<ThematicBreakBlock>().First();
        var result = _sut.Parse(divider, _inlineParser);
        Assert.IsType<DividerBlock>(result.Block);
    }

    [Fact]
    public void Parse_Underscores_ReturnsDividerBlock()
    {
        var doc = Md.Parse("___");
        var divider = doc.OfType<ThematicBreakBlock>().First();
        var result = _sut.Parse(divider, _inlineParser);
        Assert.IsType<DividerBlock>(result.Block);
    }

    [Fact]
    public void Parse_HasNoChildren()
    {
        var doc = Md.Parse("---");
        var divider = doc.OfType<ThematicBreakBlock>().First();
        var result = _sut.Parse(divider, _inlineParser);
        Assert.Empty(result.Children);
    }
}
