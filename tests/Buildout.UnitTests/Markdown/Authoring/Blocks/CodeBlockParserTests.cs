using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring.Blocks;
using Buildout.Core.Markdown.Authoring.Inline;
using Markdig.Syntax;
using NSubstitute;
using Xunit;
using Md = Markdig.Markdown;
using CodeBlk = Buildout.Core.Buildin.Models.CodeBlock;

namespace Buildout.UnitTests.Markdown.Authoring.Blocks;

public class CodeBlockParserTests
{
    private readonly CodeBlockParser _sut = new();
    private readonly IInlineMarkdownParser _inlineParser = Substitute.For<IInlineMarkdownParser>();

    [Fact]
    public void CanParse_FencedCodeBlock_ReturnsTrue()
    {
        var doc = Md.Parse("```\ncode\n```");
        var code = doc.OfType<FencedCodeBlock>().First();
        Assert.True(_sut.CanParse(code));
    }

    [Fact]
    public void CanParse_ParagraphBlock_ReturnsFalse()
    {
        var doc = Md.Parse("hello");
        var para = doc.OfType<Markdig.Syntax.ParagraphBlock>().First();
        Assert.False(_sut.CanParse(para));
    }

    [Fact]
    public void Parse_WithLanguage_ReturnsCodeBlockWithLanguage()
    {
        var doc = Md.Parse("```csharp\nConsole.WriteLine();\n```");
        var code = doc.OfType<FencedCodeBlock>().First();
        var result = _sut.Parse(code, _inlineParser);
        var block = Assert.IsType<CodeBlk>(result.Block);
        Assert.Equal("code", block.Type);
        Assert.Equal("csharp", block.Language);
    }

    [Fact]
    public void Parse_WithoutLanguage_ReturnsCodeBlockWithNullLanguage()
    {
        var doc = Md.Parse("```\nplain code\n```");
        var code = doc.OfType<FencedCodeBlock>().First();
        var result = _sut.Parse(code, _inlineParser);
        var block = Assert.IsType<CodeBlk>(result.Block);
        Assert.Null(block.Language);
    }

    [Fact]
    public void Parse_ContainsRichTextContent()
    {
        var doc = Md.Parse("```\nhello\n```");
        var code = doc.OfType<FencedCodeBlock>().First();
        var result = _sut.Parse(code, _inlineParser);
        var block = (CodeBlk)result.Block;
        Assert.NotNull(block.RichTextContent);
        Assert.NotEmpty(block.RichTextContent);
        Assert.Contains("hello", block.RichTextContent[0].Content);
    }
}
