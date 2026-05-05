using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown;

public class MentionToMarkdownRegistryTests
{
    private static IMentionToMarkdownConverter MockConverter(Type mentionClrType, string mentionType = "test")
    {
        var converter = Substitute.For<IMentionToMarkdownConverter>();
        converter.MentionClrType.Returns(mentionClrType);
        converter.MentionType.Returns(mentionType);
        return converter;
    }

    [Fact]
    public void Resolve_ReturnsConverter_WhenMentionClrTypeRegistered()
    {
        var converter = MockConverter(typeof(PageMention), "page");
        var registry = new MentionToMarkdownRegistry([converter]);

        var result = registry.Resolve(new PageMention { PageId = "abc" });

        Assert.Same(converter, result);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenMentionClrTypeNotRegistered()
    {
        var converter = MockConverter(typeof(PageMention), "page");
        var registry = new MentionToMarkdownRegistry([converter]);

        var result = registry.Resolve(new UserMention { UserId = "123" });

        Assert.Null(result);
    }

    [Fact]
    public void Constructor_Throws_OnDuplicateMentionClrType()
    {
        var first = MockConverter(typeof(PageMention), "page_a");
        var second = MockConverter(typeof(PageMention), "page_b");

        Assert.Throws<InvalidOperationException>(() =>
            new MentionToMarkdownRegistry([first, second]));
    }
}
