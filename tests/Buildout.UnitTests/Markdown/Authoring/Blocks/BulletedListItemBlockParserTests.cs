using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Blocks;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;
using NSubstitute;
using Xunit;
using Md = Markdig.Markdown;

namespace Buildout.UnitTests.Markdown.Authoring.Blocks;

public class BulletedListItemBlockParserTests
{
    private readonly BulletedListItemBlockParser _sut = new();

    private static (ListBlock list, IInlineMarkdownParser inlineParser) ParseList(string markdown)
    {
        var pipeline = new Markdig.MarkdownPipelineBuilder().Build();
        var doc = Md.Parse(markdown, pipeline);
        var list = doc.OfType<ListBlock>().First();
        var inlineParser = Substitute.For<IInlineMarkdownParser>();
        inlineParser.ParseInlines(Arg.Any<Markdig.Syntax.Inlines.ContainerInline>())
            .Returns(call =>
            {
                var container = call.Arg<Markdig.Syntax.Inlines.ContainerInline>();
                var text = string.Join("", container.OfType<Markdig.Syntax.Inlines.LiteralInline>().Select(l => l.Content.ToString()));
                return new List<RichText> { new() { Type = "text", Content = text } };
            });
        return (list, inlineParser);
    }

    [Fact]
    public void CanParse_BulletedListItem_ReturnsTrue()
    {
        var (list, _) = ParseList("- item");
        var item = list.OfType<ListItemBlock>().First();
        Assert.True(_sut.CanParse(item));
    }

    [Fact]
    public void CanParse_NumberedListItem_ReturnsFalse()
    {
        var pipeline = new Markdig.MarkdownPipelineBuilder().Build();
        var doc = Md.Parse("1. item", pipeline);
        var list = doc.OfType<ListBlock>().First();
        var item = list.OfType<ListItemBlock>().First();
        Assert.False(_sut.CanParse(item));
    }

    [Fact]
    public void Parse_SingleItem_ReturnsBulletedListItemBlock()
    {
        var (list, inlineParser) = ParseList("- one");
        var item = list.OfType<ListItemBlock>().First();
        var result = _sut.Parse(item, inlineParser);
        var block = Assert.IsType<BulletedListItemBlock>(result.Block);
        Assert.Equal("bulleted_list_item", block.Type);
        Assert.NotNull(block.RichTextContent);
        Assert.Single(block.RichTextContent);
        Assert.Equal("one", block.RichTextContent[0].Content);
    }

    [Fact]
    public void Parse_NestedSubBullets_ReturnsChildren()
    {
        var (list, inlineParser) = ParseList("- parent\n  - child1\n  - child2");
        var item = list.OfType<ListItemBlock>().First();
        var result = _sut.Parse(item, inlineParser);
        Assert.IsType<BulletedListItemBlock>(result.Block);
        Assert.NotEmpty(result.Children);
        Assert.Equal(2, result.Children.Count);
        Assert.All(result.Children, c => Assert.Equal("bulleted_list_item", c.Block.Type));
    }

    [Fact]
    public void Parse_NestedOrderedSublist_ReturnsNumberedChildren()
    {
        var (list, inlineParser) = ParseList("- parent\n  1. first\n  2. second");
        var item = list.OfType<ListItemBlock>().First();
        var result = _sut.Parse(item, inlineParser);
        Assert.NotEmpty(result.Children);
        Assert.All(result.Children, c => Assert.Equal("numbered_list_item", c.Block.Type));
    }
}
