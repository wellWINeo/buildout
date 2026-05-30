using System.Text.Json;
using System.Text.Json.Serialization;

namespace Buildout.Core.PageTree.Rendering;

public sealed class JsonTreeRenderer : ITreeRenderer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public TreeFormat Format => TreeFormat.Json;

    public string Render(TreeNode root)
    {
        var dto = ToDto(root);
        return JsonSerializer.Serialize(dto, Options) + "\n";
    }

    private static TreeNodeDto ToDto(TreeNode node) =>
        new(node.Name, node.Uri, node.Children.Select(ToDto).ToArray());

    private sealed record TreeNodeDto(
        [property: JsonPropertyOrder(0)] string Name,
        [property: JsonPropertyOrder(1)] string Uri,
        [property: JsonPropertyOrder(2)] TreeNodeDto[] Children);
}
