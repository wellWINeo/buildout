using System.Text.Json;
using Buildout.Core.Buildin.Models;
using Gen = Buildout.Core.Buildin.Generated.Models;
using Microsoft.Kiota.Abstractions.Serialization;

namespace Buildout.Core.Buildin.Mapping;

internal static class DatabaseMapper
{
    public static Database Map(Gen.Database gen)
    {
        return new Database
        {
            Id = gen.Id?.ToString() ?? string.Empty,
            CreatedAt = gen.CreatedTime,
            LastEditedAt = gen.LastEditedTime,
            CreatedBy = UserMapper.Map(gen.CreatedBy),
            LastEditedBy = UserMapper.Map(gen.LastEditedBy),
            Cover = gen.Cover?.External?.Url,
            Icon = ParentIconMapper.MapIcon(gen.Icon),
            Parent = ParentIconMapper.MapParent(gen.Parent),
            Title = gen.Title?.Select(RichTextMapper.Map).ToList(),
            Properties = MapProperties(gen.Properties),
            IsInline = gen.IsInline,
            Archived = gen.Archived ?? false,
            Url = gen.Url
        };
    }

    public static QueryDatabaseResult MapQueryResponse(Gen.QueryDatabaseResponse? gen)
    {
        if (gen is null) return new QueryDatabaseResult();

        var rows = new List<Dictionary<string, PropertyValue>>();
        if (gen.Results is UntypedArray array)
        {
            foreach (var item in array.GetValue())
            {
                if (item is null) continue;

                var element = MappingHelpers.SerializeToElement(item);
                if (!element.TryGetProperty("properties", out var propsEl))
                    continue;

                rows.Add(MapPropertyValues(propsEl));
            }
        }

        return new QueryDatabaseResult
        {
            Results = rows,
            HasMore = gen.HasMore ?? false,
            NextCursor = gen.NextCursor
        };
    }

    public static Dictionary<string, PropertySchema>? MapProperties(Gen.Database_properties? gen)
    {
        if (gen is null) return null;

        var element = MappingHelpers.SerializeToElement(gen);
        var dict = new Dictionary<string, PropertySchema>();
        foreach (var prop in element.EnumerateObject())
        {
            var schema = TryMapPropertySchema(prop.Name, prop.Value);
            if (schema is not null)
                dict[prop.Name] = schema;
        }
        return dict.Count > 0 ? dict : null;
    }

    private static PropertySchema? TryMapPropertySchema(string name, JsonElement el)
    {
        if (!el.TryGetProperty("type", out var typeEl)) return null;

        return typeEl.GetString() switch
        {
            "title" => new TitlePropertySchema { Name = name },
            "rich_text" => new RichTextPropertySchema { Name = name },
            "number" => new NumberPropertySchema { Name = name },
            "select" => MapSelectPropertySchema(name, el),
            "multi_select" => MapMultiSelectPropertySchema(name, el),
            "date" => new DatePropertySchema { Name = name },
            "formula" => new FormulaPropertySchema { Name = name },
            "relation" => new RelationPropertySchema { Name = name },
            "rollup" => new RollupPropertySchema { Name = name },
            "people" => new PeoplePropertySchema { Name = name },
            "files" => new FilesPropertySchema { Name = name },
            "checkbox" => new CheckboxPropertySchema { Name = name },
            "url" => new UrlPropertySchema { Name = name },
            "email" => new EmailPropertySchema { Name = name },
            "phone_number" => new PhonePropertySchema { Name = name },
            "created_time" => new CreatedTimePropertySchema { Name = name },
            _ => null
        };
    }

    private static SelectPropertySchema MapSelectPropertySchema(string name, JsonElement el)
    {
        var options = new List<SelectOption>();
        if (el.TryGetProperty("select", out var selEl) &&
            selEl.TryGetProperty("options", out var optsEl) &&
            optsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var opt in optsEl.EnumerateArray())
            {
                var o = ParseSelectOption(opt);
                if (o is not null) options.Add(o);
            }
        }
        return new SelectPropertySchema { Name = name, Options = options };
    }

    private static MultiSelectPropertySchema MapMultiSelectPropertySchema(string name, JsonElement el)
    {
        var options = new List<SelectOption>();
        if (el.TryGetProperty("multi_select", out var msEl) &&
            msEl.TryGetProperty("options", out var optsEl) &&
            optsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var opt in optsEl.EnumerateArray())
            {
                var o = ParseSelectOption(opt);
                if (o is not null) options.Add(o);
            }
        }
        return new MultiSelectPropertySchema { Name = name, Options = options };
    }

    private static Dictionary<string, PropertyValue> MapPropertyValues(JsonElement propsEl)
    {
        var dict = new Dictionary<string, PropertyValue>();
        foreach (var prop in propsEl.EnumerateObject())
        {
            var pv = TryMapPropertyValue(prop.Value);
            if (pv is not null)
                dict[prop.Name] = pv;
        }
        return dict;
    }

    private static PropertyValue? TryMapPropertyValue(JsonElement el)
    {
        if (!el.TryGetProperty("type", out var typeEl))
            return null;

        return typeEl.GetString() switch
        {
            "title" => MapTitleValue(el),
            "rich_text" => MapRichTextPropertyValue(el),
            "number" => MapNumberValue(el),
            "select" => MapSelectValue(el),
            "multi_select" => MapMultiSelectValue(el),
            "date" => MapDateValue(el),
            "checkbox" => MapCheckboxValue(el),
            "url" => MapUrlValue(el),
            "people" => MapPeopleValue(el),
            "relation" => MapRelationValue(el),
            _ => null
        };
    }

    private static TitlePropertyValue MapTitleValue(JsonElement el)
    {
        var items = RichTextMapper.ParseRichTextArray(el, "title");
        return new TitlePropertyValue { Title = items };
    }

    private static RichTextPropertyValue MapRichTextPropertyValue(JsonElement el)
    {
        var items = RichTextMapper.ParseRichTextArray(el, "rich_text");
        return new RichTextPropertyValue { RichText = items };
    }

    private static NumberPropertyValue MapNumberValue(JsonElement el)
    {
        double? number = null;
        if (el.TryGetProperty("number", out var numEl) && numEl.ValueKind == JsonValueKind.Number)
            number = numEl.GetDouble();
        return new NumberPropertyValue { Number = number };
    }

    private static SelectPropertyValue MapSelectValue(JsonElement el)
    {
        SelectOption? option = null;
        if (el.TryGetProperty("select", out var selEl) && selEl.ValueKind == JsonValueKind.Object)
            option = ParseSelectOption(selEl);
        return new SelectPropertyValue { Select = option };
    }

    private static MultiSelectPropertyValue MapMultiSelectValue(JsonElement el)
    {
        var options = new List<SelectOption>();
        if (el.TryGetProperty("multi_select", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                var opt = ParseSelectOption(item);
                if (opt is not null) options.Add(opt);
            }
        }
        return new MultiSelectPropertyValue { MultiSelect = options };
    }

    private static SelectOption? ParseSelectOption(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        var name = el.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
        var id = el.TryGetProperty("id", out var i) ? i.GetString() ?? string.Empty : string.Empty;
        var color = el.TryGetProperty("color", out var c) ? c.GetString() : null;
        return new SelectOption { Id = id, Name = name, Color = color };
    }

    private static DatePropertyValue MapDateValue(JsonElement el)
    {
        DateRange? range = null;
        if (el.TryGetProperty("date", out var dateEl) && dateEl.ValueKind == JsonValueKind.Object)
        {
            var start = dateEl.TryGetProperty("start", out var s) ? s.GetString() : null;
            var end = dateEl.TryGetProperty("end", out var e) ? e.GetString() : null;
            range = new DateRange { Start = start, End = end };
        }
        return new DatePropertyValue { Date = range };
    }

    private static CheckboxPropertyValue MapCheckboxValue(JsonElement el)
    {
        bool? val = null;
        if (el.TryGetProperty("checkbox", out var cbEl) && cbEl.ValueKind is JsonValueKind.True or JsonValueKind.False)
            val = cbEl.GetBoolean();
        return new CheckboxPropertyValue { Checkbox = val };
    }

    private static UrlPropertyValue MapUrlValue(JsonElement el)
    {
        string? url = null;
        if (el.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
            url = urlEl.GetString();
        return new UrlPropertyValue { Url = url };
    }

    private static PeoplePropertyValue MapPeopleValue(JsonElement el)
    {
        var people = new List<User>();
        if (el.TryGetProperty("people", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var i) ? i.GetString() ?? string.Empty : string.Empty;
                var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                var type = item.TryGetProperty("type", out var t) ? t.GetString() ?? "person" : "person";
                people.Add(new User { Id = id, Name = name, Type = type });
            }
        }
        return new PeoplePropertyValue { People = people };
    }

    private static RelationPropertyValue MapRelationValue(JsonElement el)
    {
        var ids = new List<string>();
        if (el.TryGetProperty("relation", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var i))
                    ids.Add(i.GetString() ?? string.Empty);
            }
        }
        return new RelationPropertyValue { RelationIds = ids };
    }
}
