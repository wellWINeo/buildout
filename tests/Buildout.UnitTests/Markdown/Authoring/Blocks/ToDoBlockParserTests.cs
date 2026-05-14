using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Blocks;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using NSubstitute;
using Xunit;
using Md = Markdig.Markdown;

namespace Buildout.UnitTests.Markdown.Authoring.Blocks;

public class ToDoBlockParserTests
{
    private readonly ToDoBlockParser _sut = new();

    private static (ListBlock list, IInlineMarkdownParser inlineParser) ParseTodo(string markdown)
    {
        var pipeline = new global::Markdig.MarkdownPipelineBuilder().UseTaskLists().Build();
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
    public void CanParse_TaskListItem_ReturnsTrue()
    {
        var (list, _) = ParseTodo("- [ ] task");
        var item = list.OfType<ListItemBlock>().First();
        Assert.True(_sut.CanParse(item));
    }

    [Fact]
    public void CanParse_RegularBulletItem_ReturnsFalse()
    {
        var pipeline = new global::Markdig.MarkdownPipelineBuilder().UseTaskLists().Build();
        var doc = Md.Parse("- regular item", pipeline);
        var list = doc.OfType<ListBlock>().First();
        var item = list.OfType<ListItemBlock>().First();
        Assert.False(_sut.CanParse(item));
    }

    [Fact]
    public void Parse_UncheckedTask_ReturnsToDoWithCheckedFalse()
    {
        var (list, inlineParser) = ParseTodo("- [ ] unchecked task");
        var item = list.OfType<ListItemBlock>().First();
        var result = _sut.Parse(item, inlineParser);
        var todo = Assert.IsType<ToDoBlock>(result.Block);
        Assert.Equal("to_do", todo.Type);
        Assert.False(todo.Checked);
    }

    [Fact]
    public void Parse_CheckedTask_ReturnsToDoWithCheckedTrue()
    {
        var (list, inlineParser) = ParseTodo("- [x] checked task");
        var item = list.OfType<ListItemBlock>().First();
        var result = _sut.Parse(item, inlineParser);
        var todo = Assert.IsType<ToDoBlock>(result.Block);
        Assert.True(todo.Checked);
    }

    [Fact]
    public void Parse_TaskHasRichTextContent()
    {
        var (list, inlineParser) = ParseTodo("- [ ] buy milk");
        var item = list.OfType<ListItemBlock>().First();
        var result = _sut.Parse(item, inlineParser);
        var todo = (ToDoBlock)result.Block;
        Assert.NotNull(todo.RichTextContent);
        Assert.NotEmpty(todo.RichTextContent);
    }
}
