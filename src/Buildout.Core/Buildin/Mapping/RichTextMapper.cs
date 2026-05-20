using System.Text.Json;
using Buildout.Core.Buildin.Models;
using Gen = Buildout.Core.Buildin.Generated.Models;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;

namespace Buildout.Core.Buildin.Mapping;

internal static class RichTextMapper
{
    public static RichText Map(Gen.RichTextItem gen)
    {
        return new RichText
        {
            Type = gen.Type?.ToString() ?? "text",
            Content = gen.PlainText ?? string.Empty,
            Href = gen.Href,
            Annotations = gen.Annotations is not null
                ? new Annotations
                {
                    Bold = gen.Annotations.Bold ?? false,
                    Italic = gen.Annotations.Italic ?? false,
                    Strikethrough = gen.Annotations.Strikethrough ?? false,
                    Underline = gen.Annotations.Underline ?? false,
                    Code = gen.Annotations.Code ?? false,
                    Color = gen.Annotations.Color?.ToString() ?? "default"
                }
                : null,
            Mention = MapMention(gen.Mention, gen.Type)
        };
    }

    public static Mention? MapMention(Gen.RichTextItem_mention? mention, Gen.RichTextItem_type? richTextType)
    {
        if (richTextType != Gen.RichTextItem_type.Mention || mention is null)
            return null;

        return mention.Type switch
        {
            Gen.RichTextItem_mention_type.Page => new PageMention
            {
                PageId = mention.Page?.Id?.ToString() ?? string.Empty
            },
            Gen.RichTextItem_mention_type.User => new UserMention
            {
                UserId = mention.User?.Id?.ToString() ?? string.Empty
            },
            Gen.RichTextItem_mention_type.Date => new DateMention
            {
                Start = mention.Date?.Start ?? string.Empty,
                End = mention.Date?.End
            },
            _ => null
        };
    }

    public static List<RichText> ParseRichTextArray(JsonElement el, string fieldName)
    {
        var items = new List<RichText>();
        if (!el.TryGetProperty(fieldName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return items;

        foreach (var item in arr.EnumerateArray())
        {
            var node = new JsonParseNode(item);
            var genItem = node.GetObjectValue(Gen.RichTextItem.CreateFromDiscriminatorValue);
            if (genItem is not null)
                items.Add(Map(genItem));
        }
        return items;
    }
}
