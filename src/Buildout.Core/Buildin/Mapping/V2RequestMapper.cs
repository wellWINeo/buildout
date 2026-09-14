using System.Text.Json;
using System.Text.Json.Nodes;
using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Buildin.Mapping;

/// <summary>
/// The only place where domain write models are turned into Buildin API V2 JSON.
/// Keeping this boundary explicit prevents domain-only members (for example
/// <c>plain_text</c> and block metadata) from leaking into request DTOs.
/// </summary>
internal static class V2RequestMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static JsonObject CreatePage(CreatePageRequest request)
    {
        var result = new JsonObject();

        // V2's create-page parent union has only page_id and database_id.
        // Workspace-root creation is represented by omitting parent entirely.
        switch (request.Parent)
        {
            case ParentPage page:
                result["parent"] = new JsonObject { ["page_id"] = page.Id };
                break;
            case ParentDatabase database:
                result["parent"] = new JsonObject { ["database_id"] = database.Id };
                break;
            case ParentWorkspace:
                break;
            case ParentBlock:
                throw new ArgumentException("V2 page creation does not support a block parent.", nameof(request));
            default:
                throw new ArgumentException("Unsupported page parent.", nameof(request));
        }

        result["properties"] = Properties(request.Properties);

        if (request.Children is not null)
            result["children"] = new JsonArray(request.Children.Select(BlockValue).ToArray());

        return result;
    }

    public static JsonObject CreateDatabase(CreateDatabaseRequest request)
    {
        var result = new JsonObject
        {
            ["title"] = new JsonArray(request.Title.Select(RichTextValue).ToArray()),
            ["properties"] = Schemas(request.Properties)
        };

        switch (request.Parent)
        {
            case ParentPage page:
                result["parent"] = new JsonObject { ["page_id"] = page.Id };
                break;
            case ParentWorkspace:
                break;
            default:
                throw new ArgumentException("V2 database creation supports only a page or workspace parent.", nameof(request));
        }

        return result;
    }

    public static JsonObject UpdateDatabase(UpdateDatabaseRequest request)
    {
        var result = new JsonObject();
        if (request.Title is not null)
            result["title"] = new JsonArray(request.Title.Select(RichTextValue).ToArray());
        if (request.Properties is not null)
            result["properties"] = Schemas(request.Properties);
        return result;
    }

    public static JsonObject UpdateBlock(UpdateBlockRequest request)
    {
        var result = new JsonObject { ["type"] = request.Type };
        var content = new JsonObject();
        if (request.RichTextContent is not null)
            content["rich_text"] = new JsonArray(request.RichTextContent.Select(RichTextValue).ToArray());
        AddString(content, "language", request.Language);
        AddString(content, "url", request.Url);
        if (request.Checked is not null)
            content["checked"] = request.Checked.Value;

        if (request.Type == "divider")
            result[request.Type] = new JsonObject();
        else
            result[request.Type] = content;

        if (request.InTrash is not null)
            result["in_trash"] = request.InTrash.Value;
        return result;
    }

    public static JsonObject AppendBlockChildren(AppendBlockChildrenRequest request)
        => new() { ["children"] = new JsonArray(request.Children.Select(BlockValue).ToArray()) };

    public static JsonObject QueryDatabase(QueryDatabaseRequest request)
    {
        var result = new JsonObject();
        AddJson(result, "filter", request.Filter);
        if (request.Sorts is { Count: > 0 })
            result["sorts"] = new JsonArray(request.Sorts.Select(SortValue).ToArray());
        AddString(result, "start_cursor", request.StartCursor);
        if (request.PageSize is not null)
            result["page_size"] = request.PageSize.Value;
        return result;
    }

    public static JsonObject Search(SearchRequest request)
    {
        var result = new JsonObject();
        AddString(result, "query", request.Query);
        if (request.Filter is not null)
        {
            var filter = new JsonObject();
            AddString(filter, "value", request.Filter.Value);
            AddString(filter, "property", request.Filter.Property);
            result["filter"] = filter;
        }
        if (request.Sort is not null)
        {
            var sort = new JsonObject();
            AddString(sort, "direction", request.Sort.Direction);
            AddString(sort, "timestamp", request.Sort.Timestamp);
            result["sort"] = sort;
        }
        AddString(result, "start_cursor", request.StartCursor);
        if (request.PageSize is not null)
            result["page_size"] = request.PageSize.Value;
        return result;
    }

    private static JsonObject BlockValue(Block block)
    {
        var result = new JsonObject
        {
            ["object"] = "block",
            ["type"] = block.Type
        };
        var content = new JsonObject();

        switch (block)
        {
            case ParagraphBlock paragraph:
                TextContent(content, paragraph.RichTextContent);
                break;
            case Heading1Block heading:
                TextContent(content, heading.RichTextContent);
                break;
            case Heading2Block heading:
                TextContent(content, heading.RichTextContent);
                break;
            case Heading3Block heading:
                TextContent(content, heading.RichTextContent);
                break;
            case BulletedListItemBlock list:
                TextContent(content, list.RichTextContent);
                break;
            case NumberedListItemBlock list:
                TextContent(content, list.RichTextContent);
                break;
            case ToDoBlock todo:
                TextContent(content, todo.RichTextContent);
                if (todo.Checked is not null) content["checked"] = todo.Checked.Value;
                break;
            case ToggleBlock toggle:
                TextContent(content, toggle.RichTextContent);
                break;
            case CodeBlock code:
                TextContent(content, code.RichTextContent);
                AddString(content, "language", code.Language);
                break;
            case QuoteBlock quote:
                TextContent(content, quote.RichTextContent);
                break;
            case DividerBlock:
                break;
            case ImageBlock image:
                AddString(content, "url", image.Url);
                if (image.Caption is not null)
                    content["caption"] = new JsonArray(image.Caption.Select(RichTextValue).ToArray());
                break;
            case EmbedBlock embed:
                AddString(content, "url", embed.Url);
                break;
            case TableBlock table:
                if (table.TableWidth is not null) content["table_width"] = table.TableWidth.Value;
                if (table.HasColumnHeader is not null) content["has_column_header"] = table.HasColumnHeader.Value;
                if (table.HasRowHeader is not null) content["has_row_header"] = table.HasRowHeader.Value;
                break;
            case TableRowBlock row:
                if (row.Cells is not null)
                    content["cells"] = new JsonArray(row.Cells.Select(c => new JsonArray(c.Select(RichTextValue).ToArray())).ToArray());
                break;
            case ChildPageBlock childPage:
                AddString(content, "title", childPage.Title);
                break;
            case ChildDatabaseBlock childDatabase:
                AddString(content, "title", childDatabase.Title);
                break;
            case SyncedBlock synced:
                if (!string.IsNullOrWhiteSpace(synced.SyncedFromId))
                    content["synced_from"] = new JsonObject { ["block_id"] = synced.SyncedFromId };
                break;
            case LinkPreviewBlock linkPreview:
                AddString(content, "url", linkPreview.Url);
                break;
            default:
                throw new ArgumentException($"Block type {block.GetType().Name} cannot be sent to V2.", nameof(block));
        }

        result[block.Type] = content;
        return result;
    }

    private static void TextContent(JsonObject target, IReadOnlyList<RichText>? values)
    {
        if (values is not null)
            target["rich_text"] = new JsonArray(values.Select(RichTextValue).ToArray());
    }

    private static JsonObject RichTextValue(RichText value)
    {
        var result = new JsonObject { ["type"] = value.Type };

        if (value.Type == "mention")
        {
            if (value.Mention is null)
                throw new ArgumentException("Mention rich text must have a mention value.", nameof(value));
            result["mention"] = MentionValue(value.Mention);
        }
        else if (value.Type == "equation")
        {
            result["equation"] = new JsonObject { ["expression"] = value.Content };
        }
        else
        {
            var text = new JsonObject { ["content"] = value.Content };
            if (value.Href is not null)
                text["link"] = new JsonObject { ["url"] = value.Href };
            result["text"] = text;
        }

        var annotations = value.Annotations ?? new Annotations();
        result["annotations"] = new JsonObject
        {
            ["bold"] = annotations.Bold,
            ["italic"] = annotations.Italic,
            ["strikethrough"] = annotations.Strikethrough,
            ["underline"] = annotations.Underline,
            ["code"] = annotations.Code,
            ["text_color"] = annotations.Color,
            ["background_color"] = annotations.BackgroundColor
        };

        return result;
    }

    private static JsonObject MentionValue(Mention mention) => mention switch
    {
        PageMention page => new JsonObject { ["type"] = "page", ["page"] = new JsonObject { ["id"] = page.PageId } },
        UserMention user => new JsonObject
        {
            ["type"] = "user",
            ["user"] = new JsonObject { ["object"] = "user", ["id"] = user.UserId }
        },
        DateMention date => MentionDate(date),
        _ => throw new ArgumentException("Unsupported mention type.", nameof(mention))
    };

    private static JsonObject MentionDate(DateMention value)
    {
        var date = new JsonObject();
        AddRequiredString(date, "start", value.Start, nameof(value));
        // end is required by the V2 DTO and nullable.
        date["end"] = value.End;
        return new JsonObject { ["type"] = "date", ["date"] = date };
    }

    private static JsonObject Properties(IReadOnlyDictionary<string, PropertyValue> values)
    {
        var result = new JsonObject();
        foreach (var (name, value) in values)
            result[name] = Property(value);
        return result;
    }

    private static JsonObject Property(PropertyValue value)
    {
        var result = new JsonObject { ["type"] = value.Type };
        AddString(result, "id", value.Id);
        switch (value)
        {
            case TitlePropertyValue title:
                result["title"] = new JsonArray(title.Title?.Select(RichTextValue).ToArray() ?? []);
                break;
            case RichTextPropertyValue richText:
                result["rich_text"] = new JsonArray(richText.RichText?.Select(RichTextValue).ToArray() ?? []);
                break;
            case NumberPropertyValue number:
                result["number"] = number.Number;
                break;
            case SelectPropertyValue select:
                result["select"] = select.Select is null ? null : Option(select.Select);
                break;
            case MultiSelectPropertyValue multi:
                result["multi_select"] = new JsonArray(multi.MultiSelect?.Select(Option).ToArray() ?? []);
                break;
            case DatePropertyValue date:
                result["date"] = date.Date is null ? null : Date(date.Date);
                break;
            case FormulaPropertyValue formula:
                result["formula"] = Formula(formula);
                break;
            case RelationPropertyValue relation:
                result["relation"] = new JsonArray(relation.RelationIds?.Select(id => (JsonNode)new JsonObject { ["id"] = id }).ToArray() ?? []);
                break;
            case PeoplePropertyValue people:
                result["people"] = new JsonArray(people.People?.Select(person => (JsonNode)new JsonObject { ["id"] = person.Id }).ToArray() ?? []);
                break;
            case FilesPropertyValue files:
                result["files"] = new JsonArray(files.Files?.Select(FileValue).ToArray() ?? []);
                break;
            case CheckboxPropertyValue checkbox:
                result["checkbox"] = checkbox.Checkbox ?? false;
                break;
            case UrlPropertyValue url:
                result["url"] = url.Url;
                break;
            case RollupPropertyValue rollup:
                result["rollup"] = new JsonObject
                {
                    ["type"] = "array",
                    ["array"] = new JsonArray(rollup.RollupResults?.Select(Property).ToArray() ?? [])
                };
                break;
            default:
                throw new ArgumentException($"Unsupported property value {value.GetType().Name}.", nameof(value));
        }
        return result;
    }

    private static JsonObject Schemas(IReadOnlyDictionary<string, PropertySchema> values)
    {
        var result = new JsonObject();
        foreach (var (name, schema) in values)
        {
            result[name] = Schema(name, schema);
        }
        return result;
    }

    private static JsonObject Schema(string name, PropertySchema schema)
    {
        var definition = new JsonObject
        {
            ["name"] = name,
            ["type"] = schema.Type
        };
        switch (schema)
        {
            case NumberPropertySchema number:
                if (number.Format is not null)
                    definition["number"] = new JsonObject { ["format"] = number.Format };
                break;
            case SelectPropertySchema select:
                if (select.Options is not null)
                    definition["select"] = new JsonObject { ["options"] = new JsonArray(select.Options.Select(Option).ToArray()) };
                break;
            case MultiSelectPropertySchema multi:
                if (multi.Options is not null)
                    definition["multi_select"] = new JsonObject { ["options"] = new JsonArray(multi.Options.Select(Option).ToArray()) };
                break;
            case FormulaPropertySchema formula:
                if (formula.Expression is not null)
                    definition["formula"] = new JsonObject { ["expression"] = formula.Expression };
                break;
            case RelationPropertySchema relation:
                if (relation.DatabaseId is not null)
                    definition["relation"] = new JsonObject { ["database_id"] = relation.DatabaseId };
                break;
            case RollupPropertySchema rollup:
                var rollupDefinition = new JsonObject();
                AddString(rollupDefinition, "relation_property_id", rollup.RelationName);
                AddString(rollupDefinition, "rollup_property_id", rollup.RollupPropertyName);
                AddString(rollupDefinition, "function", rollup.Function);
                if (rollupDefinition.Count > 0)
                    definition["rollup"] = rollupDefinition;
                break;
        }
        return definition;
    }

    private static JsonObject Option(SelectOption option)
    {
        var result = new JsonObject { ["name"] = option.Name };
        AddString(result, "id", option.Id);
        AddString(result, "color", option.Color);
        return result;
    }

    private static JsonObject FileValue(FileObject value)
    {
        var result = new JsonObject
        {
            ["name"] = value.Name ?? value.Id,
            ["type"] = "external"
        };
        if (value.Url is null)
            throw new ArgumentException("A file property value must have a URL.", nameof(value));
        result["external"] = new JsonObject { ["url"] = value.Url };
        return result;
    }

    private static JsonObject Date(DateRange value)
    {
        var result = new JsonObject();
        AddRequiredString(result, "start", value.Start, nameof(value));
        // DateValue.end is required by V2 even when it is null.
        result["end"] = value.End;
        return result;
    }

    private static JsonObject Formula(FormulaPropertyValue value)
    {
        var result = new JsonObject { ["type"] = FormulaType(value) };
        AddString(result, "string", value.StringResult);
        if (value.NumberResult is not null) result["number"] = value.NumberResult.Value;
        if (value.BooleanResult is not null) result["boolean"] = value.BooleanResult.Value;
        if (value.DateResult is not null) result["date"] = Date(value.DateResult);
        return result;
    }

    private static string FormulaType(FormulaPropertyValue value)
        => value.DateResult is not null ? "date" :
            value.BooleanResult is not null ? "boolean" :
            value.NumberResult is not null ? "number" : "string";

    private static JsonObject SortValue(Sort value)
    {
        var result = new JsonObject();
        AddString(result, "property", value.Property);
        AddString(result, "direction", value.Direction);
        AddString(result, "timestamp", value.Timestamp);
        return result;
    }

    private static void AddString(JsonObject target, string name, string? value)
    {
        if (value is not null)
            target[name] = value;
    }

    private static void AddRequiredString(JsonObject target, string name, string? value, string parameterName)
    {
        if (value is null)
            throw new ArgumentException($"V2 request field '{name}' is required.", parameterName);
        target[name] = value;
    }

    private static void AddJson(JsonObject target, string name, object? value)
    {
        if (value is null) return;
        var node = JsonSerializer.SerializeToNode(value, JsonOptions);
        if (node is JsonObject jsonObject)
            RemoveNullObjectProperties(jsonObject);
        target[name] = node;
    }

    private static void RemoveNullObjectProperties(JsonObject value)
    {
        foreach (var property in value.ToArray())
        {
            if (property.Value is null)
            {
                value.Remove(property.Key);
                continue;
            }

            if (property.Value is JsonObject child)
                RemoveNullObjectProperties(child);
            else if (property.Value is JsonArray array)
            {
                foreach (var item in array.OfType<JsonObject>())
                    RemoveNullObjectProperties(item);
            }
        }
    }
}
