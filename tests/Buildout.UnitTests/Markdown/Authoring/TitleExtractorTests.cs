using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig;
using Markdig.Syntax;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown.Authoring;

public class TitleExtractorTests
{
    private static (string? title, MarkdownDocument doc) Extract(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var doc = Markdig.Markdown.Parse(markdown, pipeline);
        var inlineParser = Substitute.For<IInlineMarkdownParser>();
        inlineParser.ParseInlines(Arg.Any<Markdig.Syntax.Inlines.ContainerInline>())
            .Returns(call =>
            {
                var container = call.Arg<Markdig.Syntax.Inlines.ContainerInline>();
                var text = string.Join("", container.OfType<Markdig.Syntax.Inlines.LiteralInline>().Select(l => l.Content.ToString()));
                return new List<RichText> { new() { Type = "text", Content = text } };
            });
        return TitleExtractor.Extract(doc, inlineParser);
    }

    [Fact]
    public void H1First_ReturnsTitleAndRemovesFromDoc()
    {
        var (title, doc) = Extract("# My Title\n\nBody text");
        Assert.Equal("My Title", title);
        Assert.Single(doc);
    }

    [Fact]
    public void H1NotFirst_StaysAsBodyBlock()
    {
        var (title, doc) = Extract("Intro text\n\n# Heading\n\nMore");
        Assert.Null(title);
    }

    [Fact]
    public void NoH1_ReturnsNullTitle()
    {
        var (title, _) = Extract("## Not a title\n\nBody");
        Assert.Null(title);
    }

    [Fact]
    public void H1OnlyDocument_EmptyBody()
    {
        var (title, doc) = Extract("# Only Title");
        Assert.Equal("Only Title", title);
        Assert.Empty(doc);
    }

    [Fact]
    public void MultipleH1s_OnlyFirstConsumed()
    {
        var (title, _) = Extract("# First\n\n## Body\n\n# Second");
        Assert.Equal("First", title);
    }

    [Fact]
    public void EmptyDocument_ReturnsNullTitle()
    {
        var (title, doc) = Extract("");
        Assert.Null(title);
        Assert.Empty(doc);
    }
}
