using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Internal;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown;

public class InlineRendererTests
{
    private static InlineRenderer CreateSut(params IMentionToMarkdownConverter[] converters)
    {
        return new InlineRenderer(new MentionToMarkdownRegistry(converters));
    }

    [Fact]
    public void Render_NullInput_ReturnsEmptyString()
    {
        var sut = CreateSut();
        var result = sut.Render(null, 0);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Render_EmptyInput_ReturnsEmptyString()
    {
        var sut = CreateSut();
        var result = sut.Render([], 0);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Render_PlainText_ReturnsContent()
    {
        var sut = CreateSut();
        var items = new List<RichText>
        {
            new() { Type = "text", Content = "hello world" }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void Render_Bold_WrapsInDoubleAsterisks()
    {
        var sut = CreateSut();
        var items = new List<RichText>
        {
            new()
            {
                Type = "text",
                Content = "bold",
                Annotations = new Annotations { Bold = true }
            }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("**bold**", result);
    }

    [Fact]
    public void Render_Italic_WrapsInSingleAsterisks()
    {
        var sut = CreateSut();
        var items = new List<RichText>
        {
            new()
            {
                Type = "text",
                Content = "italic",
                Annotations = new Annotations { Italic = true }
            }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("*italic*", result);
    }

    [Fact]
    public void Render_Strikethrough_WrapsInDoubleTildes()
    {
        var sut = CreateSut();
        var items = new List<RichText>
        {
            new()
            {
                Type = "text",
                Content = "struck",
                Annotations = new Annotations { Strikethrough = true }
            }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("~~struck~~", result);
    }

    [Fact]
    public void Render_Code_WrapsInBackticks()
    {
        var sut = CreateSut();
        var items = new List<RichText>
        {
            new()
            {
                Type = "text",
                Content = "var x = 1;",
                Annotations = new Annotations { Code = true }
            }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("`var x = 1;`", result);
    }

    [Fact]
    public void Render_Underline_DroppedWithoutWrapping()
    {
        var sut = CreateSut();
        var items = new List<RichText>
        {
            new()
            {
                Type = "text",
                Content = "underlined",
                Annotations = new Annotations { Underline = true }
            }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("underlined", result);
    }

    [Fact]
    public void Render_BoldItalicStacked_TripleAsterisks()
    {
        var sut = CreateSut();
        var items = new List<RichText>
        {
            new()
            {
                Type = "text",
                Content = "both",
                Annotations = new Annotations { Bold = true, Italic = true }
            }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("***both***", result);
    }

    [Fact]
    public void Render_BoldStrikethroughStacked_CorrectOrder()
    {
        var sut = CreateSut();
        var items = new List<RichText>
        {
            new()
            {
                Type = "text",
                Content = "text",
                Annotations = new Annotations { Bold = true, Strikethrough = true }
            }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("**~~text~~**", result);
    }

    [Fact]
    public void Render_BoldItalicStrikethroughCode_FullStack()
    {
        var sut = CreateSut();
        var items = new List<RichText>
        {
            new()
            {
                Type = "text",
                Content = "x",
                Annotations = new Annotations
                {
                    Bold = true, Italic = true, Strikethrough = true, Code = true
                }
            }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("***~~`x`~~***", result);
    }

    [Fact]
    public void Render_LinkWithHref_MarkdownLink()
    {
        var sut = CreateSut();
        var items = new List<RichText>
        {
            new()
            {
                Type = "text",
                Content = "click here",
                Href = "https://example.com"
            }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("[click here](https://example.com)", result);
    }

    [Fact]
    public void Render_LinkWithAnnotations_AnnotatedLinkText()
    {
        var sut = CreateSut();
        var items = new List<RichText>
        {
            new()
            {
                Type = "text",
                Content = "link",
                Href = "https://example.com",
                Annotations = new Annotations { Bold = true }
            }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("[**link**](https://example.com)", result);
    }

    [Fact]
    public void Render_MultipleItems_Concatenated()
    {
        var sut = CreateSut();
        var items = new List<RichText>
        {
            new() { Type = "text", Content = "Hello " },
            new()
            {
                Type = "text",
                Content = "world",
                Annotations = new Annotations { Bold = true }
            }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("Hello **world**", result);
    }

    [Fact]
    public void Render_MentionWithConverter_UsesConverter()
    {
        var converter = Substitute.For<IMentionToMarkdownConverter>();
        converter.MentionClrType.Returns(typeof(PageMention));
        converter.MentionType.Returns("page");
        converter.Render(Arg.Any<Mention>(), Arg.Any<string>())
            .Returns(call => $"[{call.Arg<string>()}](buildin://{((PageMention)call.Arg<Mention>()).PageId})");

        var sut = CreateSut(converter);
        var items = new List<RichText>
        {
            new()
            {
                Type = "mention",
                Content = "My Page",
                Mention = new PageMention { PageId = "abc123" }
            }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("[My Page](buildin://abc123)", result);
    }

    [Fact]
    public void Render_MentionWithoutConverter_FallsBackToContent()
    {
        var sut = CreateSut();
        var items = new List<RichText>
        {
            new()
            {
                Type = "mention",
                Content = "Unknown",
                Mention = new DateMention { Start = "2024-01-01" }
            }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("Unknown", result);
    }

    [Fact]
    public void Render_EquationType_FallsBackToContent()
    {
        var sut = CreateSut();
        var items = new List<RichText>
        {
            new() { Type = "equation", Content = "E = mc^2" }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("E = mc^2", result);
    }

    [Fact]
    public void Render_UnknownType_FallsBackToContent()
    {
        var sut = CreateSut();
        var items = new List<RichText>
        {
            new() { Type = "something_else", Content = "fallback text" }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("fallback text", result);
    }

    [Fact]
    public void Render_MentionWithAnnotationsConverter_AnnotatedMentionOutput()
    {
        var converter = Substitute.For<IMentionToMarkdownConverter>();
        converter.MentionClrType.Returns(typeof(UserMention));
        converter.MentionType.Returns("user");
        converter.Render(Arg.Any<Mention>(), Arg.Any<string>())
            .Returns(call => $"@{call.Arg<string>()}");

        var sut = CreateSut(converter);
        var items = new List<RichText>
        {
            new()
            {
                Type = "mention",
                Content = "John",
                Annotations = new Annotations { Bold = true },
                Mention = new UserMention { UserId = "u1", DisplayName = "John" }
            }
        };
        var result = sut.Render(items, 0);
        Assert.Equal("**@John**", result);
    }
}
