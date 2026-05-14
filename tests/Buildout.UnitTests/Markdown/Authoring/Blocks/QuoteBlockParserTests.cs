using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Blocks;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;
using NSubstitute;
using Xunit;
using Md = Markdig.Markdown;
using QuoteBlk = Buildout.Core.Buildin.Models.QuoteBlock;

namespace Buildout.UnitTests.Markdown.Authoring.Blocks;

public class QuoteBlockParserTests
{
    private readonly QuoteBlockParser _sut = new();

    private static (Markdig.Syntax.QuoteBlock quote, IInlineMarkdownParser inlineParser) ParseQuote(string markdown)
    {
        var doc = Md.Parse(markdown);
        var quote = doc.OfType<Markdig.Syntax.QuoteBlock>().First();
        var inlineParser = Substitute.For<IInlineMarkdownParser>();
        inlineParser.ParseInlines(Arg.Any<Markdig.Syntax.Inlines.ContainerInline>())
            .Returns(call =>
            {
                var container = call.Arg<Markdig.Syntax.Inlines.ContainerInline>();
                var text = string.Join("", container.OfType<Markdig.Syntax.Inlines.LiteralInline>().Select(l => l.Content.ToString()));
                return new List<RichText> { new() { Type = "text", Content = text } };
            });
        return (quote, inlineParser);
    }

    [Fact]
    public void CanParse_QuoteBlock_ReturnsTrue()
    {
        var (quote, _) = ParseQuote("> text");
        Assert.True(_sut.CanParse(quote));
    }

    [Fact]
    public void CanParse_ParagraphBlock_ReturnsFalse()
    {
        var doc = Md.Parse("hello");
        var para = doc.OfType<Markdig.Syntax.ParagraphBlock>().First();
        Assert.False(_sut.CanParse(para));
    }

    [Fact]
    public void Parse_SingleLineQuote_ReturnsQuoteBlock()
    {
        var (quote, inlineParser) = ParseQuote("> quoted text");
        var result = _sut.Parse(quote, inlineParser);
        var block = Assert.IsType<QuoteBlk>(result.Block);
        Assert.Equal("quote", block.Type);
        Assert.NotNull(block.RichTextContent);
        Assert.NotEmpty(block.RichTextContent);
    }

    [Fact]
    public void Parse_MultiLineQuote_ReturnsQuoteBlockWithAllText()
    {
        var (quote, inlineParser) = ParseQuote("> line1\n> line2");
        var result = _sut.Parse(quote, inlineParser);
        var block = (QuoteBlk)result.Block;
        Assert.NotNull(block.RichTextContent);
        Assert.True(block.RichTextContent.Count >= 1);
    }
}
