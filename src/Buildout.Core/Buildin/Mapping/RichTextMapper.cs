using System.Text.Json;
using Buildout.Core.Buildin.Models;
using Gen = Buildout.Core.Buildin.Generated.Models;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;

namespace Buildout.Core.Buildin.Mapping;

internal static class RichTextMapper
{
    public static RichText Map(JsonElement element)
    {
        var type = String(element, "type") ?? "text";
        var content = String(element, "plain_text")
            ?? (element.TryGetProperty("text", out var text) ? String(text, "content") : null)
            ?? (element.TryGetProperty("equation", out var equation) ? String(equation, "expression") : null)
            ?? string.Empty;

        var href = String(element, "href");
        if (href is null && element.TryGetProperty("text", out var textValue) &&
            textValue.TryGetProperty("link", out var link) && link.ValueKind == JsonValueKind.Object)
            href = String(link, "url");

        Annotations? annotations = null;
        if (element.TryGetProperty("annotations", out var annotationValue) && annotationValue.ValueKind == JsonValueKind.Object)
        {
            annotations = new Annotations
            {
                Bold = Bool(annotationValue, "bold"),
                Italic = Bool(annotationValue, "italic"),
                Strikethrough = Bool(annotationValue, "strikethrough"),
                Underline = Bool(annotationValue, "underline"),
                Code = Bool(annotationValue, "code"),
                Color = String(annotationValue, "color") ?? "default",
                BackgroundColor = String(annotationValue, "background_color") ?? "default"
            };
        }

        return new RichText
        {
            Type = type,
            Content = content,
            Href = href,
            Annotations = annotations,
            Mention = type == "mention" ? MapMention(element) : null
        };
    }

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

    public static Gen.RichTextItem MapToGen(RichText rt)
    {
        return new Gen.RichTextItem
        {
            Type = Gen.RichTextItem_type.Text,
            PlainText = rt.Content,
            Text = new Gen.RichTextItem_text { Content = rt.Content }
        };
    }

    public static List<RichText> ParseRichTextArray(JsonElement el, string fieldName)
    {
        var items = new List<RichText>();
        if (!el.TryGetProperty(fieldName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return items;

        foreach (var item in arr.EnumerateArray())
            items.Add(Map(item));
        return items;
    }

    public static List<RichText> ParseArray(JsonElement array)
        => array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Select(Map).ToList()
            : [];

    private static Mention? MapMention(JsonElement element)
    {
        if (!element.TryGetProperty("mention", out var mention) || mention.ValueKind != JsonValueKind.Object)
            return null;

        return String(mention, "type") switch
        {
            "page" when mention.TryGetProperty("page", out var page) => new PageMention
            {
                PageId = String(page, "id") ?? string.Empty
            },
            "database" when mention.TryGetProperty("database", out var database) => new DatabaseMention
            {
                DatabaseId = String(database, "id") ?? string.Empty
            },
            "user" when mention.TryGetProperty("user", out var user) => new UserMention
            {
                UserId = String(user, "id") ?? string.Empty,
                DisplayName = String(user, "name")
            },
            "date" when mention.TryGetProperty("date", out var date) => new DateMention
            {
                Start = String(date, "start") ?? string.Empty,
                End = String(date, "end")
            },
            _ => null
        };
    }

    private static string? String(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool Bool(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) &&
           value.ValueKind is (JsonValueKind.True or JsonValueKind.False) &&
           value.GetBoolean();
}
