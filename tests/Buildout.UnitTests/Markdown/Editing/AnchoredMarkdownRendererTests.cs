using System.Text.RegularExpressions;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Conversion.Mentions;
using Buildout.Core.Markdown.Editing.Internal;
using Buildout.Core.Markdown.Internal;
using Xunit;

namespace Buildout.UnitTests.Markdown.Editing;

public partial class AnchoredMarkdownRendererTests
{
    private readonly AnchoredMarkdownRenderer _sut;
    private readonly BlockToMarkdownRegistry _registry;

    public AnchoredMarkdownRendererTests()
    {
        var blockConverters = new IBlockToMarkdownConverter[]
        {
            new ParagraphConverter(),
            new Heading1Converter(),
            new Heading2Converter(),
            new Heading3Converter(),
            new BulletedListItemConverter(),
            new NumberedListItemConverter(),
            new ToDoConverter(),
            new CodeConverter(),
            new QuoteConverter(),
            new DividerConverter()
        };
        var mentionConverters = new IMentionToMarkdownConverter[]
        {
            new PageMentionConverter(),
            new DatabaseMentionConverter(),
            new UserMentionConverter(),
            new DateMentionConverter()
        };
        _registry = new BlockToMarkdownRegistry(blockConverters);
        var mentionRegistry = new MentionToMarkdownRegistry(mentionConverters);
        var inlineRenderer = new InlineRenderer(mentionRegistry);
        _sut = new AnchoredMarkdownRenderer(inlineRenderer, _registry);
    }

    private static BlockSubtree Subtree(Block block, IReadOnlyList<BlockSubtree>? children = null)
        => new() { Block = block, Children = children ?? [] };

    private static RichText Text(string content)
        => new() { Type = "text", Content = content };

    [Fact]
    public void Render_RootSentinel_IsVeryFirstLine()
    {
        var tree = new[]
        {
            Subtree(new ParagraphBlock { Id = "p1", RichTextContent = [Text("Hello")] })
        };

        var (markdown, _) = _sut.Render(tree);

        var firstLine = markdown.Split('\n')[0].TrimEnd('\r');
        Assert.Equal("<!-- buildin:root -->", firstLine);
    }

    [Fact]
    public void Render_ParagraphBlock_EmitsBlockAnchor()
    {
        var tree = new[]
        {
            Subtree(new ParagraphBlock { Id = "para-1", RichTextContent = [Text("Hello world")] })
        };

        var (markdown, _) = _sut.Render(tree);

        Assert.Contains("<!-- buildin:block:para-1 -->", markdown);
        Assert.Contains("Hello world", markdown);
    }

    [Fact]
    public void Render_Heading1Block_EmitsBlockAnchor()
    {
        var tree = new[]
        {
            Subtree(new Heading1Block { Id = "h1-1", RichTextContent = [Text("Title")] })
        };

        var (markdown, _) = _sut.Render(tree);

        Assert.Contains("<!-- buildin:block:h1-1 -->", markdown);
        Assert.Contains("## Title", markdown);
    }

    [Fact]
    public void Render_Heading2Block_EmitsBlockAnchor()
    {
        var tree = new[]
        {
            Subtree(new Heading2Block { Id = "h2-1", RichTextContent = [Text("Subtitle")] })
        };

        var (markdown, _) = _sut.Render(tree);

        Assert.Contains("<!-- buildin:block:h2-1 -->", markdown);
    }

    [Fact]
    public void Render_Heading3Block_EmitsBlockAnchor()
    {
        var tree = new[]
        {
            Subtree(new Heading3Block { Id = "h3-1", RichTextContent = [Text("Sub-subtitle")] })
        };

        var (markdown, _) = _sut.Render(tree);

        Assert.Contains("<!-- buildin:block:h3-1 -->", markdown);
    }

    [Fact]
    public void Render_BulletedListItemBlock_EmitsBlockAnchor()
    {
        var tree = new[]
        {
            Subtree(new BulletedListItemBlock { Id = "bli-1", RichTextContent = [Text("Item")] })
        };

        var (markdown, _) = _sut.Render(tree);

        Assert.Contains("<!-- buildin:block:bli-1 -->", markdown);
        Assert.Contains("- Item", markdown);
    }

    [Fact]
    public void Render_NumberedListItemBlock_EmitsBlockAnchor()
    {
        var tree = new[]
        {
            Subtree(new NumberedListItemBlock { Id = "nli-1", RichTextContent = [Text("First")] })
        };

        var (markdown, _) = _sut.Render(tree);

        Assert.Contains("<!-- buildin:block:nli-1 -->", markdown);
        Assert.Contains("1. First", markdown);
    }

    [Fact]
    public void Render_ToDoBlock_EmitsBlockAnchor()
    {
        var tree = new[]
        {
            Subtree(new ToDoBlock { Id = "todo-1", RichTextContent = [Text("Task")], Checked = true })
        };

        var (markdown, _) = _sut.Render(tree);

        Assert.Contains("<!-- buildin:block:todo-1 -->", markdown);
        Assert.Contains("- [x] Task", markdown);
    }

    [Fact]
    public void Render_CodeBlock_EmitsBlockAnchor()
    {
        var tree = new[]
        {
            Subtree(new CodeBlock { Id = "code-1", RichTextContent = [Text("var x = 1;")], Language = "csharp" })
        };

        var (markdown, _) = _sut.Render(tree);

        Assert.Contains("<!-- buildin:block:code-1 -->", markdown);
        Assert.Contains("```csharp", markdown);
    }

    [Fact]
    public void Render_QuoteBlock_EmitsBlockAnchor()
    {
        var tree = new[]
        {
            Subtree(new QuoteBlock { Id = "quote-1", RichTextContent = [Text("Quoted text")] })
        };

        var (markdown, _) = _sut.Render(tree);

        Assert.Contains("<!-- buildin:block:quote-1 -->", markdown);
        Assert.Contains("> Quoted text", markdown);
    }

    [Fact]
    public void Render_DividerBlock_EmitsBlockAnchor()
    {
        var tree = new[]
        {
            Subtree(new DividerBlock { Id = "div-1" })
        };

        var (markdown, _) = _sut.Render(tree);

        Assert.Contains("<!-- buildin:block:div-1 -->", markdown);
        Assert.Contains("---", markdown);
    }

    [Fact]
    public void Render_ChildPageBlock_EmitsOpaqueAnchorWithPlaceholder()
    {
        var tree = new[]
        {
            Subtree(new ChildPageBlock { Id = "cp-1", Title = "My Sub-page" })
        };

        var result = _sut.Render(tree);

        Assert.Contains("<!-- buildin:opaque:cp-1 -->", result.Markdown);
        Assert.Contains("cp-1", result.UnknownBlockIds);
    }

    [Fact]
    public void Render_ChildDatabaseBlock_EmitsOpaqueAnchorWithPlaceholder()
    {
        var tree = new[]
        {
            Subtree(new ChildDatabaseBlock { Id = "cdb-1", Title = "My DB" })
        };

        var result = _sut.Render(tree);

        Assert.Contains("<!-- buildin:opaque:cdb-1 -->", result.Markdown);
        Assert.Contains("cdb-1", result.UnknownBlockIds);
    }

    [Fact]
    public void Render_UnsupportedBlock_EmitsOpaqueAnchorWithPlaceholder()
    {
        var tree = new[]
        {
            Subtree(new UnsupportedBlock { Id = "unsup-1" })
        };

        var result = _sut.Render(tree);

        Assert.Contains("<!-- buildin:opaque:unsup-1 -->", result.Markdown);
        Assert.Contains("unsup-1", result.UnknownBlockIds);
    }

    [Fact]
    public void Render_OpaqueBlock_ContainsPlaceholderParagraph()
    {
        var tree = new[]
        {
            Subtree(new ChildPageBlock { Id = "cp-1", Title = "My Sub-page" })
        };

        var (markdown, _) = _sut.Render(tree);

        var anchorIndex = markdown.IndexOf("<!-- buildin:opaque:cp-1 -->", StringComparison.Ordinal);
        Assert.True(anchorIndex >= 0);

        var afterAnchor = markdown[anchorIndex..];
        var lines = afterAnchor.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        Assert.True(lines.Count >= 2);
        Assert.True(lines[1].Length > 0);
    }

    [Fact]
    public void Render_AnchorStrippedEqualsPageMarkdownRendererOutput()
    {
        var tree = new[]
        {
            Subtree(new ParagraphBlock { Id = "p1", RichTextContent = [Text("First paragraph")] }),
            Subtree(new Heading1Block { Id = "h1", RichTextContent = [Text("Heading")] }),
            Subtree(new ParagraphBlock { Id = "p2", RichTextContent = [Text("Second paragraph")] })
        };

        var (anchored, _) = _sut.Render(tree);

        var stripped = AnchorCommentRegex().Replace(anchored, "").Trim();

        var pageRendererOutput = RenderUnanchored(tree);

        Assert.Equal(
            NormalizeLineEndings(pageRendererOutput),
            NormalizeLineEndings(stripped));
    }

    [Fact]
    public void Render_AnchorStrippedWithUnsupportedBlock_EqualsPageMarkdownRendererOutput()
    {
        var tree = new BlockSubtree[]
        {
            Subtree(new ParagraphBlock { Id = "p1", RichTextContent = [Text("Before")] }),
            Subtree(new ChildPageBlock { Id = "cp-1", Title = "Sub-page" }),
            Subtree(new ParagraphBlock { Id = "p2", RichTextContent = [Text("After")] })
        };

        var (anchored, _) = _sut.Render(tree);

        var stripped = AnchorCommentRegex().Replace(anchored, "").Trim();

        var pageRendererOutput = RenderUnanchored(tree);

        Assert.Equal(
            NormalizeLineEndings(pageRendererOutput),
            NormalizeLineEndings(stripped));
    }

    [Fact]
    public void Render_NestedBlocks_AnchorsAtCorrectDepth()
    {
        var tree = new[]
        {
            Subtree(
                new BulletedListItemBlock { Id = "parent-bli", RichTextContent = [Text("Parent item")] },
                [
                    Subtree(new ParagraphBlock { Id = "child-p1", RichTextContent = [Text("Nested paragraph")] })
                ])
        };

        var (markdown, _) = _sut.Render(tree);

        var lines = markdown.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        var parentAnchorLine = lines.FirstOrDefault(l => l.Contains("<!-- buildin:block:parent-bli -->"));
        var childAnchorLine = lines.FirstOrDefault(l => l.Contains("<!-- buildin:block:child-p1 -->"));

        Assert.NotNull(parentAnchorLine);
        Assert.NotNull(childAnchorLine);
        Assert.Equal(0, parentAnchorLine.Length - parentAnchorLine.TrimStart().Length);
        Assert.StartsWith("  ", childAnchorLine);
    }

    [Fact]
    public void Render_DeeplyNested_AnchorsAtCorrectDepth()
    {
        var tree = new[]
        {
            Subtree(
                new BulletedListItemBlock { Id = "level-0", RichTextContent = [Text("L0")] },
                [
                    Subtree(
                        new BulletedListItemBlock { Id = "level-1", RichTextContent = [Text("L1")] },
                        [
                            Subtree(new ParagraphBlock { Id = "level-2", RichTextContent = [Text("L2")] })
                        ])
                ])
        };

        var (markdown, _) = _sut.Render(tree);

        var lines = markdown.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        var l0Line = lines.First(l => l.Contains("<!-- buildin:block:level-0 -->"));
        var l1Line = lines.First(l => l.Contains("<!-- buildin:block:level-1 -->"));
        var l2Line = lines.First(l => l.Contains("<!-- buildin:block:level-2 -->"));

        Assert.Equal(0, l0Line.Length - l0Line.TrimStart().Length);
        Assert.StartsWith("  ", l1Line);
        Assert.StartsWith("    ", l2Line);
    }

    [Fact]
    public void Render_MultipleRootBlocks_EachGetsAnchor()
    {
        var tree = new[]
        {
            Subtree(new ParagraphBlock { Id = "a", RichTextContent = [Text("A")] }),
            Subtree(new ParagraphBlock { Id = "b", RichTextContent = [Text("B")] }),
            Subtree(new ParagraphBlock { Id = "c", RichTextContent = [Text("C")] })
        };

        var (markdown, _) = _sut.Render(tree);

        Assert.Contains("<!-- buildin:block:a -->", markdown);
        Assert.Contains("<!-- buildin:block:b -->", markdown);
        Assert.Contains("<!-- buildin:block:c -->", markdown);
    }

    [Fact]
    public void Render_UnknownBlockIds_ContainsAllOpaqueIds()
    {
        var tree = new[]
        {
            Subtree(new ParagraphBlock { Id = "p1", RichTextContent = [Text("Supported")] }),
            Subtree(new ChildPageBlock { Id = "cp-1", Title = "Page" }),
            Subtree(new ChildDatabaseBlock { Id = "cdb-1", Title = "DB" }),
            Subtree(new UnsupportedBlock { Id = "unsup-1" })
        };

        var (_, unknownIds) = _sut.Render(tree);

        Assert.Equal(3, unknownIds.Count);
        Assert.Contains("cp-1", unknownIds);
        Assert.Contains("cdb-1", unknownIds);
        Assert.Contains("unsup-1", unknownIds);
    }

    [Fact]
    public void Render_AllSupportedBlockTypes_NoUnknownBlockIds()
    {
        var tree = new BlockSubtree[]
        {
            Subtree(new ParagraphBlock { Id = "p1", RichTextContent = [Text("Text")] }),
            Subtree(new Heading1Block { Id = "h1", RichTextContent = [Text("H1")] }),
            Subtree(new Heading2Block { Id = "h2", RichTextContent = [Text("H2")] }),
            Subtree(new Heading3Block { Id = "h3", RichTextContent = [Text("H3")] }),
            Subtree(new BulletedListItemBlock { Id = "bli", RichTextContent = [Text("Bullet")] }),
            Subtree(new NumberedListItemBlock { Id = "nli", RichTextContent = [Text("Number")] }),
            Subtree(new ToDoBlock { Id = "todo", RichTextContent = [Text("Task")] }),
            Subtree(new CodeBlock { Id = "code", RichTextContent = [Text("code")], Language = "js" }),
            Subtree(new QuoteBlock { Id = "quote", RichTextContent = [Text("Quote")] }),
            Subtree(new DividerBlock { Id = "divider" })
        };

        var (_, unknownIds) = _sut.Render(tree);

        Assert.Empty(unknownIds);
    }

    [Fact]
    public void Render_EachSupportedBlock_AnchorPrecedesContent()
    {
        var tree = new[]
        {
            Subtree(new ParagraphBlock { Id = "p-abc", RichTextContent = [Text("Body text")] })
        };

        var (markdown, _) = _sut.Render(tree);

        var anchorPos = markdown.IndexOf("<!-- buildin:block:p-abc -->", StringComparison.Ordinal);
        var contentPos = markdown.IndexOf("Body text", StringComparison.Ordinal);

        Assert.True(anchorPos >= 0);
        Assert.True(contentPos >= 0);
        Assert.True(anchorPos < contentPos);
    }

    [Fact]
    public void Render_RootSentinelPrecedesAllBlockAnchors()
    {
        var tree = new[]
        {
            Subtree(new ParagraphBlock { Id = "first", RichTextContent = [Text("Content")] })
        };

        var (markdown, _) = _sut.Render(tree);

        var rootPos = markdown.IndexOf("<!-- buildin:root -->", StringComparison.Ordinal);
        var blockPos = markdown.IndexOf("<!-- buildin:block:first -->", StringComparison.Ordinal);

        Assert.True(rootPos >= 0);
        Assert.True(blockPos >= 0);
        Assert.True(rootPos < blockPos);
    }

    private string RenderUnanchored(IReadOnlyList<BlockSubtree> tree)
    {
        var writer = new MarkdownWriter();
        var mentionConverters = new IMentionToMarkdownConverter[]
        {
            new PageMentionConverter(),
            new DatabaseMentionConverter(),
            new UserMentionConverter(),
            new DateMentionConverter()
        };
        var mentionRegistry = new MentionToMarkdownRegistry(mentionConverters);
        var inlineRenderer = new InlineRenderer(mentionRegistry);
        var ctx = new MarkdownRenderContext(writer, inlineRenderer, _registry, 0);

        foreach (var subtree in tree)
            ctx.WriteBlockSubtree(subtree);

        return writer.ToString().Trim();
    }

    private static string NormalizeLineEndings(string input)
        => input.Replace("\r\n", "\n").Replace("\r", "\n").Trim();

    [GeneratedRegex(@"<!-- buildin:(root|block:[^>]+|opaque:[^>]+) -->\s*\n?", RegexOptions.None)]
    private static partial Regex AnchorCommentRegex();
}
