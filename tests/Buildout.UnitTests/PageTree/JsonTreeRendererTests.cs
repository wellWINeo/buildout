using System.Text.Json;
using Buildout.Core.PageTree;
using Buildout.Core.PageTree.Rendering;
using Xunit;

namespace Buildout.UnitTests.PageTree;

public sealed class JsonTreeRendererTests
{
    private static JsonTreeRenderer CreateSut() => new JsonTreeRenderer();

    private static TreeNode Leaf(string name, string uri = "https://x.com") =>
        new(name, uri, Array.Empty<TreeNode>());

    [Fact]
    public void SingleNode_ProducesValidJson()
    {
        var sut = CreateSut();
        var root = Leaf("Root", "https://example.com");

        var result = sut.Render(root);

        var doc = JsonDocument.Parse(result);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void PropertyOrder_IsNameUriChildren()
    {
        var sut = CreateSut();
        var root = Leaf("Root", "https://example.com");

        var result = sut.Render(root);

        var doc = JsonDocument.Parse(result);
        var props = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(["name", "uri", "children"], props);
    }

    [Fact]
    public void LeafNode_HasEmptyChildrenArray()
    {
        var sut = CreateSut();
        var root = Leaf("Root");

        var result = sut.Render(root);

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("children", out var children));
        Assert.Equal(JsonValueKind.Array, children.ValueKind);
        Assert.Empty(children.EnumerateArray());
    }

    [Fact]
    public void ChildrenPresentOnLeaves_NeverAbsent()
    {
        var sut = CreateSut();
        var root = new TreeNode("Root", "https://r.com", [Leaf("Child")]);

        var result = sut.Render(root);
        var doc = JsonDocument.Parse(result);

        var child = doc.RootElement.GetProperty("children").EnumerateArray().First();
        Assert.True(child.TryGetProperty("children", out var grandChildren));
        Assert.Equal(JsonValueKind.Array, grandChildren.ValueKind);
        Assert.Empty(grandChildren.EnumerateArray());
    }

    [Fact]
    public void RecursiveNesting_SerializesCorrectly()
    {
        var sut = CreateSut();
        var root = new TreeNode("Root", "https://r.com", [
            new TreeNode("Child", "https://c.com", [Leaf("GrandChild", "https://gc.com")])
        ]);

        var result = sut.Render(root);
        var doc = JsonDocument.Parse(result);

        var child = doc.RootElement.GetProperty("children").EnumerateArray().First();
        Assert.Equal("Child", child.GetProperty("name").GetString());

        var grandChild = child.GetProperty("children").EnumerateArray().First();
        Assert.Equal("GrandChild", grandChild.GetProperty("name").GetString());
    }

    [Fact]
    public void CamelCaseFieldNames()
    {
        var sut = CreateSut();
        var root = Leaf("Root");

        var result = sut.Render(root);
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.TryGetProperty("name", out _));
        Assert.True(doc.RootElement.TryGetProperty("uri", out _));
        Assert.True(doc.RootElement.TryGetProperty("children", out _));
        Assert.False(doc.RootElement.TryGetProperty("Name", out _));
        Assert.False(doc.RootElement.TryGetProperty("Uri", out _));
        Assert.False(doc.RootElement.TryGetProperty("Children", out _));
    }

    [Fact]
    public void Utf8Names_PreservedVerbatim()
    {
        const string unicodeName = "日本語テスト🌸";
        var sut = CreateSut();
        var root = Leaf(unicodeName);

        var result = sut.Render(root);
        var doc = JsonDocument.Parse(result);

        Assert.Equal(unicodeName, doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void NoHtmlEscaping_InNames()
    {
        var sut = CreateSut();
        var root = Leaf("<script>alert('xss')</script>");

        var result = sut.Render(root);
        var doc = JsonDocument.Parse(result);

        Assert.Equal("<script>alert('xss')</script>", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void NoMarkdownEscaping_InNames()
    {
        var sut = CreateSut();
        var root = Leaf("[bracket]name");

        var result = sut.Render(root);
        var doc = JsonDocument.Parse(result);

        Assert.Equal("[bracket]name", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void PrettyPrinted_MultipleLines()
    {
        var sut = CreateSut();
        var root = Leaf("Root");

        var result = sut.Render(root);

        Assert.Contains('\n', result);
    }

    [Fact]
    public void TrailingNewline_Present()
    {
        var sut = CreateSut();
        var root = Leaf("Root");

        var result = sut.Render(root);

        Assert.True(result.EndsWith('\n'));
    }

    [Fact]
    public void Format_IsJson()
    {
        var sut = CreateSut();
        Assert.Equal(TreeFormat.Json, sut.Format);
    }

    [Fact]
    public void NameValue_MatchesInput()
    {
        const string name = "My Page";
        var sut = CreateSut();
        var root = Leaf(name, "https://example.com");

        var result = sut.Render(root);
        var doc = JsonDocument.Parse(result);

        Assert.Equal(name, doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void UriValue_MatchesInput()
    {
        const string uri = "https://buildin.ai/workspace/page";
        var sut = CreateSut();
        var root = Leaf("Page", uri);

        var result = sut.Render(root);
        var doc = JsonDocument.Parse(result);

        Assert.Equal(uri, doc.RootElement.GetProperty("uri").GetString());
    }
}
