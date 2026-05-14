using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring;
using Xunit;

namespace Buildout.UnitTests.Markdown.Authoring;

public class MarkdownToBlocksParserTests
{
    private readonly MarkdownToBlocksParser _sut = new();

    [Fact]
    public void FullDocument_TitleAndBodyBlocks()
    {
        var markdown = "# My Page\n\nHello world\n\n## Section\n\nSome detail";
        var result = _sut.Parse(markdown);

        Assert.Equal("My Page", result.Title);
        Assert.NotEmpty(result.Body);
        Assert.Equal(3, result.Body.Count);
    }

    [Fact]
    public void NoH1_TitleIsNull()
    {
        var markdown = "Just a paragraph\n\n## Not a title";
        var result = _sut.Parse(markdown);

        Assert.Null(result.Title);
        Assert.NotEmpty(result.Body);
    }

    [Fact]
    public void EmptyDocument_TitleNullEmptyBody()
    {
        var result = _sut.Parse("");

        Assert.Null(result.Title);
        Assert.Empty(result.Body);
    }

    [Fact]
    public void ParagraphBlock()
    {
        var result = _sut.Parse("Hello world");

        Assert.Null(result.Title);
        Assert.Single(result.Body);
        Assert.IsType<ParagraphBlock>(result.Body[0].Block);
        Assert.Equal("paragraph", result.Body[0].Block.Type);
    }

    [Fact]
    public void HeadingBlock()
    {
        var result = _sut.Parse("## Section");

        Assert.Null(result.Title);
        Assert.Single(result.Body);
        Assert.IsType<Heading2Block>(result.Body[0].Block);
    }

    [Fact]
    public void BulletedList()
    {
        var result = _sut.Parse("- one\n- two\n- three");

        Assert.Null(result.Title);
        Assert.Equal(3, result.Body.Count);
        Assert.All(result.Body, b => Assert.Equal("bulleted_list_item", b.Block.Type));
    }

    [Fact]
    public void NumberedList()
    {
        var result = _sut.Parse("1. first\n2. second");

        Assert.Null(result.Title);
        Assert.Equal(2, result.Body.Count);
        Assert.All(result.Body, b => Assert.Equal("numbered_list_item", b.Block.Type));
    }

    [Fact]
    public void ToDoItems_CheckedAndUnchecked()
    {
        var result = _sut.Parse("- [x] done\n- [ ] pending");

        Assert.Equal(2, result.Body.Count);
        var done = Assert.IsType<ToDoBlock>(result.Body[0].Block);
        Assert.True(done.Checked);
        var pending = Assert.IsType<ToDoBlock>(result.Body[1].Block);
        Assert.False(pending.Checked);
    }

    [Fact]
    public void CodeBlock()
    {
        var result = _sut.Parse("```js\nconsole.log('hi')\n```");

        Assert.Single(result.Body);
        var code = Assert.IsType<CodeBlock>(result.Body[0].Block);
        Assert.Equal("js", code.Language);
    }

    [Fact]
    public void QuoteBlock()
    {
        var result = _sut.Parse("> quoted text");

        Assert.Single(result.Body);
        Assert.IsType<QuoteBlock>(result.Body[0].Block);
    }

    [Fact]
    public void DividerBlock()
    {
        var result = _sut.Parse("---");

        Assert.Single(result.Body);
        Assert.IsType<DividerBlock>(result.Body[0].Block);
    }

    [Fact]
    public void AllBlockTypesInSequence()
    {
        var markdown = "# Title\n\nParagraph text\n\n## Heading\n\n- bullet\n\n1. numbered\n\n```py\npass\n```\n\n> quote\n\n---";
        var result = _sut.Parse(markdown);

        Assert.Equal("Title", result.Title);
        Assert.Equal(7, result.Body.Count);
        Assert.Equal("paragraph", result.Body[0].Block.Type);
        Assert.Equal("heading_2", result.Body[1].Block.Type);
        Assert.Equal("bulleted_list_item", result.Body[2].Block.Type);
        Assert.Equal("numbered_list_item", result.Body[3].Block.Type);
        Assert.Equal("code", result.Body[4].Block.Type);
        Assert.Equal("quote", result.Body[5].Block.Type);
        Assert.IsType<DividerBlock>(result.Body[6].Block);
    }

    [Fact]
    public void HtmlPlaceholder_UnsupportedBlock()
    {
        var markdown = "# Title\n\n<!-- unsupported block: table -->";
        var result = _sut.Parse(markdown);

        Assert.Equal("Title", result.Title);
        Assert.Single(result.Body);
        Assert.Equal("paragraph", result.Body[0].Block.Type);
    }

    [Fact]
    public void MentionInParagraph_RecoversToMentionType()
    {
        var markdown = "# Title\n\nCheck [My Page](buildin://abc123)";
        var result = _sut.Parse(markdown);

        Assert.Equal("Title", result.Title);
        Assert.Single(result.Body);
        var para = Assert.IsType<ParagraphBlock>(result.Body[0].Block);
        Assert.NotNull(para.RichTextContent);
        Assert.NotEmpty(para.RichTextContent);
        var mention = para.RichTextContent.FirstOrDefault(r => r.Type == "mention");
        Assert.NotNull(mention);
        Assert.IsType<PageMention>(mention.Mention);
        Assert.Equal("abc123", ((PageMention)mention.Mention!).PageId);
    }

    [Fact]
    public void MixedBulletAndToDo_TodoOnlyForCheckboxes()
    {
        var markdown = "- [x] checked\n- regular bullet";
        var result = _sut.Parse(markdown);

        Assert.Equal(2, result.Body.Count);
        Assert.IsType<ToDoBlock>(result.Body[0].Block);
        Assert.IsType<BulletedListItemBlock>(result.Body[1].Block);
    }
}
