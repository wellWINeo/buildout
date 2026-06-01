using Buildout.Core.Buildin.Mapping;
using Buildout.Core.Buildin.Models;
using Gen = Buildout.Core.Buildin.Generated.Models;
using Xunit;

namespace Buildout.UnitTests.Buildin;

public sealed class RichTextMapperTests
{
    [Fact]
    public void MapToGen_SetsTypeToText()
    {
        var rt = new RichText { Type = "text", Content = "Hello" };

        var result = RichTextMapper.MapToGen(rt);

        Assert.Equal(Gen.RichTextItem_type.Text, result.Type);
    }

    [Fact]
    public void MapToGen_SetsPlainTextAndTextContent()
    {
        var rt = new RichText { Type = "text", Content = "World" };

        var result = RichTextMapper.MapToGen(rt);

        Assert.Equal("World", result.PlainText);
        Assert.Equal("World", result.Text?.Content);
    }

    [Fact]
    public void MapToGen_EmptyContent_ProducesEmptyStrings()
    {
        var rt = new RichText { Type = "text", Content = string.Empty };

        var result = RichTextMapper.MapToGen(rt);

        Assert.Equal(string.Empty, result.PlainText);
        Assert.Equal(string.Empty, result.Text?.Content);
    }
}
