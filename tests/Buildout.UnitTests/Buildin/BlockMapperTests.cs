using Buildout.Core.Buildin.Mapping;
using Buildout.Core.Buildin.Models;
using Gen = Buildout.Core.Buildin.Generated.Models;
using Xunit;

namespace Buildout.UnitTests.Buildin;

public sealed class BlockMapperTests
{
    private static readonly IReadOnlyList<RichText> _richText =
        [new RichText { Type = "text", Content = "Hello" }];

    public static IEnumerable<object[]> RichTextBlockCases() =>
    [
        [new ParagraphBlock         { RichTextContent = _richText }, Gen.AppendBlockChildrenRequest_children_type.Paragraph],
        [new Heading1Block          { RichTextContent = _richText }, Gen.AppendBlockChildrenRequest_children_type.Heading_1],
        [new Heading2Block          { RichTextContent = _richText }, Gen.AppendBlockChildrenRequest_children_type.Heading_2],
        [new Heading3Block          { RichTextContent = _richText }, Gen.AppendBlockChildrenRequest_children_type.Heading_3],
        [new BulletedListItemBlock  { RichTextContent = _richText }, Gen.AppendBlockChildrenRequest_children_type.Bulleted_list_item],
        [new NumberedListItemBlock  { RichTextContent = _richText }, Gen.AppendBlockChildrenRequest_children_type.Numbered_list_item],
        [new QuoteBlock             { RichTextContent = _richText }, Gen.AppendBlockChildrenRequest_children_type.Quote],
        [new ToggleBlock            { RichTextContent = _richText }, Gen.AppendBlockChildrenRequest_children_type.Toggle],
    ];

    [Theory]
    [MemberData(nameof(RichTextBlockCases))]
    public void MapToAppendChild_RichTextBlock_MapsTypeAndContent(
        Block block, Gen.AppendBlockChildrenRequest_children_type expectedType)
    {
        var result = BlockMapper.MapToAppendChild(block);

        Assert.Equal(expectedType, result.Type);
        Assert.NotNull(result.Data?.RichText);
        Assert.Single(result.Data.RichText);
        Assert.Equal("Hello", result.Data.RichText[0].PlainText);
        Assert.Equal("Hello", result.Data.RichText[0].Text?.Content);
    }

    [Fact]
    public void MapToAppendChild_CodeBlock_MapsLanguage()
    {
        var block = new CodeBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "console.log(\"hi\");" }],
            Language = "javascript"
        };

        var result = BlockMapper.MapToAppendChild(block);

        Assert.Equal(Gen.AppendBlockChildrenRequest_children_type.Code, result.Type);
        Assert.Equal("javascript", result.Data?.Language);
        Assert.Single(result.Data!.RichText!);
    }

    [Fact]
    public void MapToAppendChild_ToDoBlock_MapsChecked()
    {
        var block = new ToDoBlock { RichTextContent = [new RichText { Type = "text", Content = "Task" }], Checked = true };

        var result = BlockMapper.MapToAppendChild(block);

        Assert.Equal(Gen.AppendBlockChildrenRequest_children_type.To_do, result.Type);
        Assert.True(result.Data?.Checked);
    }

    [Fact]
    public void MapToAppendChild_DividerBlock_HasNoRichText()
    {
        var result = BlockMapper.MapToAppendChild(new DividerBlock());

        Assert.Equal(Gen.AppendBlockChildrenRequest_children_type.Divider, result.Type);
        Assert.Null(result.Data?.RichText);
    }

    [Fact]
    public void MapToAppendChild_ImageBlock_MapsUrl()
    {
        var block = new ImageBlock { Url = "https://example.com/img.png" };

        var result = BlockMapper.MapToAppendChild(block);

        Assert.Equal(Gen.AppendBlockChildrenRequest_children_type.Image, result.Type);
        Assert.Equal("https://example.com/img.png", result.Data?.Url);
    }

    [Fact]
    public void MapToAppendChild_UnsupportedBlock_Throws()
    {
        Assert.Throws<ArgumentException>(() => BlockMapper.MapToAppendChild(new UnsupportedBlock()));
    }

    [Fact]
    public void MapToAppendChild_NullRichTextContent_ProducesNullRichTextInData()
    {
        var block = new ParagraphBlock { RichTextContent = null };

        var result = BlockMapper.MapToAppendChild(block);

        Assert.Null(result.Data?.RichText);
    }

    [Fact]
    public void MapToUpdateRequest_ParagraphBlock_MapsTypeAndRichText()
    {
        var request = new UpdateBlockRequest
        {
            Type = "paragraph",
            RichTextContent = [new RichText { Type = "text", Content = "Hello" }]
        };

        var result = BlockMapper.MapToUpdateRequest(request);

        Assert.Equal(Gen.UpdateBlockRequest_type.Paragraph, result.Type);
        Assert.Single(result.Data!.RichText!);
        Assert.Equal("Hello", result.Data!.RichText![0].PlainText);
    }

    [Fact]
    public void MapToUpdateRequest_CodeBlock_MapsLanguage()
    {
        var request = new UpdateBlockRequest
        {
            Type = "code",
            RichTextContent = [new RichText { Type = "text", Content = "x = 1" }],
            Language = "python"
        };

        var result = BlockMapper.MapToUpdateRequest(request);

        Assert.Equal(Gen.UpdateBlockRequest_type.Code, result.Type);
        Assert.Equal("python", result.Data?.Language);
    }

    [Fact]
    public void MapToUpdateRequest_ToDoBlock_MapsChecked()
    {
        var request = new UpdateBlockRequest { Type = "to_do", Checked = true };

        var result = BlockMapper.MapToUpdateRequest(request);

        Assert.Equal(Gen.UpdateBlockRequest_type.To_do, result.Type);
        Assert.True(result.Data?.Checked);
    }

    [Fact]
    public void MapToUpdateRequest_UnknownType_Throws()
    {
        var request = new UpdateBlockRequest { Type = "unknown_xyz" };

        Assert.Throws<ArgumentException>(() => BlockMapper.MapToUpdateRequest(request));
    }
}
