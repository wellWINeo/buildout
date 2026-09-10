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
            InTrash = gen.InTrash ?? gen.Archived ?? false,
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
            InTrash = gen.InTrash ?? gen.Archived ?? false,
            Url = gen.Url
        };
    }

    public static Page Map(JsonElement element)
    {
        var properties = element.TryGetProperty("properties", out var propertyElement)
            ? DatabaseMapper.MapPropertyValues(propertyElement)
            : null;

        return new Page
        {
            Id = String(element, "id") ?? string.Empty,
            CreatedAt = Date(element, "created_time", "created_at"),
            LastEditedAt = Date(element, "last_edited_time", "last_edited_at"),
            CreatedBy = MapUser(element, "created_by"),
            LastEditedBy = MapUser(element, "last_edited_by"),
            Cover = MapUrl(element, "cover"),
            Icon = MapIcon(element),
            Parent = element.TryGetProperty("parent", out var parent) ? ParentIconMapper.MapParent(parent) : null,
            Properties = properties,
            Title = FindTitle(properties),
            InTrash = Bool(element, "in_trash") ?? Bool(element, "archived") ?? false,
            ObjectType = String(element, "object"),
            Url = String(element, "url")
        };
    }

    public static Page MapDatabaseAsPage(JsonElement element)
    {
        var database = DatabaseMapper.Map(element);
        return new Page
        {
            Id = database.Id,
            CreatedAt = database.CreatedAt,
            LastEditedAt = database.LastEditedAt,
            CreatedBy = database.CreatedBy,
            LastEditedBy = database.LastEditedBy,
            Cover = database.Cover,
            Icon = database.Icon,
            Parent = database.Parent,
            Title = database.Title,
            InTrash = database.InTrash,
            ObjectType = "database",
            Url = database.Url
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
            if (prop.Value.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "title")
                return RichTextMapper.ParseRichTextArray(prop.Value, "title");
        }
        return null;
    }

    private static IReadOnlyList<RichText>? FindTitle(Dictionary<string, PropertyValue>? properties)
    {
        if (properties is null) return null;
        foreach (var value in properties.Values)
        {
            if (value is TitlePropertyValue title)
                return title.Title;
        }
        return null;
    }

    private static UserMe? MapUser(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
            return null;
        return new UserMe
        {
            Id = String(value, "id") ?? string.Empty,
            Name = String(value, "name"),
            AvatarUrl = String(value, "avatar_url"),
            Type = String(value, "type") ?? "user",
            Email = value.TryGetProperty("person", out var person) ? String(person, "email") : null
        };
    }

    private static Icon? MapIcon(JsonElement element)
    {
        if (!element.TryGetProperty("icon", out var icon) || icon.ValueKind != JsonValueKind.Object)
            return null;
        return String(icon, "type") switch
        {
            "emoji" => new IconEmoji(String(icon, "emoji") ?? string.Empty),
            "external" when icon.TryGetProperty("external", out var external) => new IconExternal(String(external, "url") ?? string.Empty),
            "file" when icon.TryGetProperty("file", out var file) => new IconFile(String(file, "url") ?? string.Empty),
            _ => null
        };
    }

    private static string? MapUrl(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
            return null;
        return String(value, "url") ??
            (value.TryGetProperty("external", out var external) ? String(external, "url") : null) ??
            (value.TryGetProperty("file", out var file) ? String(file, "url") : null);
    }

    private static string? String(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool? Bool(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind is (JsonValueKind.True or JsonValueKind.False) ? value.GetBoolean() : null;

    private static DateTimeOffset? Date(JsonElement element, string name, string alternate)
        => DateTimeOffset.TryParse(String(element, name) ?? String(element, alternate), out var value) ? value : null;
}
