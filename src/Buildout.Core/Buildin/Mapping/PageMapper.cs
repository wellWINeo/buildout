using System.Text.Json;
using Buildout.Core.Buildin.Models;
using Gen = Buildout.Core.Buildin.Generated.Models;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;

namespace Buildout.Core.Buildin.Mapping;

internal static class PageMapper
{
    public static Page Map(Gen.Page gen)
    {
        return new Page
        {
            Id = gen.Id?.ToString() ?? string.Empty,
            CreatedAt = gen.CreatedTime,
            LastEditedAt = gen.LastEditedTime,
            CreatedBy = UserMapper.Map(gen.CreatedBy),
            LastEditedBy = UserMapper.Map(gen.LastEditedBy),
            Cover = gen.Cover?.External?.Url,
            Icon = ParentIconMapper.MapIcon(gen.Icon),
            Parent = ParentIconMapper.MapParent(gen.Parent),
            Archived = gen.Archived ?? false,
            Url = gen.Url,
            Title = ExtractTitle(gen.Properties)
        };
    }

    public static Page Map(Gen.CreatePageResponse gen)
    {
        return new Page
        {
            Id = gen.Id?.ToString() ?? string.Empty,
            CreatedAt = gen.CreatedAt,
            LastEditedAt = gen.UpdatedAt,
            Archived = gen.Archived ?? false,
            Url = gen.Url
        };
    }

    public static List<RichText>? ExtractTitle(Gen.Page_properties? properties)
    {
        if (properties is null) return null;

        using var writer = new JsonSerializationWriter();
        writer.WriteObjectValue(null, (IParsable)properties);
        using var stream = writer.GetSerializedContent();
        using var doc = JsonDocument.Parse(stream);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.TryGetProperty("type", out var typeEl)
                && typeEl.ValueKind == JsonValueKind.String
                && typeEl.GetString() == "title"
                && prop.Value.TryGetProperty("title", out var titleEl)
                && titleEl.ValueKind == JsonValueKind.Array)
            {
                return RichTextMapper.ParseRichTextArray(prop.Value, "title");
            }
        }

        return null;
    }
}
