using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown;

public class BlockToMarkdownRegistryTests
{
    private static IBlockToMarkdownConverter MockConverter(Type blockClrType, string blockType = "test")
    {
        var converter = Substitute.For<IBlockToMarkdownConverter>();
        converter.BlockClrType.Returns(blockClrType);
        converter.BlockType.Returns(blockType);
        converter.RecurseChildren.Returns(false);
        return converter;
    }

    [Fact]
    public void Resolve_ReturnsConverter_WhenBlockClrTypeRegistered()
    {
        var converter = MockConverter(typeof(ParagraphBlock), "paragraph");
        var registry = new BlockToMarkdownRegistry([converter]);

        var result = registry.Resolve(new ParagraphBlock());

        Assert.Same(converter, result);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenBlockClrTypeNotRegistered()
    {
        var converter = MockConverter(typeof(Heading1Block), "heading_1");
        var registry = new BlockToMarkdownRegistry([converter]);

        var result = registry.Resolve(new ParagraphBlock());

        Assert.Null(result);
    }

    [Fact]
    public void Constructor_Throws_OnDuplicateBlockClrType()
    {
        var first = MockConverter(typeof(ParagraphBlock), "paragraph_a");
        var second = MockConverter(typeof(ParagraphBlock), "paragraph_b");

        Assert.Throws<InvalidOperationException>(() =>
            new BlockToMarkdownRegistry([first, second]));
    }
}
