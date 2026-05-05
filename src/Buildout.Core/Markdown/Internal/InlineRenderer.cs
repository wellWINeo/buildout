using System.Text;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;

namespace Buildout.Core.Markdown.Internal;

public sealed class InlineRenderer : IInlineRenderer
{
    private readonly MentionToMarkdownRegistry _registry;

    public InlineRenderer(MentionToMarkdownRegistry registry)
    {
        _registry = registry;
    }

    public string Render(IReadOnlyList<RichText>? items, int indentLevel)
    {
        if (items is null or { Count: 0 })
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var item in items)
        {
            sb.Append(RenderItem(item));
        }

        return sb.ToString();
    }

    private string RenderItem(RichText item)
    {
        var text = item.Type switch
        {
            "mention" => RenderMention(item),
            _ => item.Content
        };

        text = ApplyAnnotations(text, item.Annotations);

        if (item.Href is not null)
            text = $"[{text}]({item.Href})";

        return text;
    }

    private string RenderMention(RichText item)
    {
        if (item.Mention is null)
            return item.Content;

        var converter = _registry.Resolve(item.Mention);
        if (converter is null)
            return item.Content;

        return converter.Render(item.Mention, item.Content);
    }

    private static string ApplyAnnotations(string text, Annotations? annotations)
    {
        if (annotations is null)
            return text;

        if (annotations.Code)
            text = $"`{text}`";
        if (annotations.Strikethrough)
            text = $"~~{text}~~";
        if (annotations.Italic)
            text = $"*{text}*";
        if (annotations.Bold)
            text = $"**{text}**";

        return text;
    }
}
