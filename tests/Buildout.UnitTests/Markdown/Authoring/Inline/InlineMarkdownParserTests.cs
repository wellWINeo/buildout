using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Xunit;
using static Markdig.Markdown;

namespace Buildout.UnitTests.Markdown.Authoring.Inline;

public class InlineMarkdownParserTests
{
    private readonly InlineMarkdownParser _sut = new();

    private static ContainerInline ParseInlines(string markdown)
    {
        var pipeline = new Markdig.MarkdownPipelineBuilder().Build();
        var doc = Parse(markdown, pipeline);
        var para = doc.OfType<Markdig.Syntax.ParagraphBlock>().First();
        return para.Inline!;
    }

    [Fact]
    public void PlainText_ReturnsSingleRichText()
    {
        var result = _sut.ParseInlines(ParseInlines("hello world"));
        Assert.Single(result);
        Assert.Equal("text", result[0].Type);
        Assert.Equal("hello world", result[0].Content);
    }

    [Fact]
    public void BoldText_ReturnsBoldAnnotation()
    {
        var result = _sut.ParseInlines(ParseInlines("**bold**"));
        Assert.Single(result);
        Assert.True(result[0].Annotations?.Bold);
        Assert.Equal("bold", result[0].Content);
    }

    [Fact]
    public void ItalicText_ReturnsItalicAnnotation()
    {
        var result = _sut.ParseInlines(ParseInlines("*italic*"));
        Assert.Single(result);
        Assert.True(result[0].Annotations?.Italic);
        Assert.Equal("italic", result[0].Content);
    }

    [Fact]
    public void InlineCode_ReturnsCodeAnnotation()
    {
        var result = _sut.ParseInlines(ParseInlines("`code`"));
        Assert.Single(result);
        Assert.True(result[0].Annotations?.Code);
        Assert.Equal("code", result[0].Content);
    }

    [Fact]
    public void PlainHttpLink_ReturnsLinkWithHref()
    {
        var result = _sut.ParseInlines(ParseInlines("[click](https://example.com)"));
        Assert.Single(result);
        Assert.Equal("text", result[0].Type);
        Assert.Equal("https://example.com", result[0].Href);
        Assert.Equal("click", result[0].Content);
    }

    [Fact]
    public void BuildinLink_ReturnsMentionRichText()
    {
        var result = _sut.ParseInlines(ParseInlines("[My Page](buildin://abc123)"));
        Assert.Single(result);
        Assert.Equal("mention", result[0].Type);
        Assert.Equal("My Page", result[0].Content);
        Assert.IsType<PageMention>(result[0].Mention);
        Assert.Equal("abc123", ((PageMention)result[0].Mention!).PageId);
    }

    [Fact]
    public void HardLineBreak_ReturnsNewlineInRun()
    {
        var result = _sut.ParseInlines(ParseInlines("line1  \nline2"));
        Assert.Contains(result, r => r.Content == "\n");
    }

    [Fact]
    public void MixedFormatting_ReturnsMultipleRuns()
    {
        var result = _sut.ParseInlines(ParseInlines("plain **bold** end"));
        Assert.Equal(3, result.Count);
        Assert.Equal("plain ", result[0].Content);
        Assert.True(result[1].Annotations?.Bold);
        Assert.Equal(" end", result[2].Content);
    }
}
