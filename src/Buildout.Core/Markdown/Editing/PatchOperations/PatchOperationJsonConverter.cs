using System.Text.Json;
using System.Text.Json.Serialization;

namespace Buildout.Core.Markdown.Editing.PatchOperations;

public sealed class PatchOperationJsonConverter : JsonConverter<PatchOperation>
{
    private const string Discriminator = "op";

    public override PatchOperation? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty(Discriminator, out var opElement))
            throw new JsonException("Missing 'op' discriminator field.");

        var op = opElement.GetString()
            ?? throw new JsonException("'op' discriminator field must not be null.");

        var json = root.GetRawText();

        return op switch
        {
            "replace_block" => JsonSerializer.Deserialize<ReplaceBlockOperation>(json, options),
            "replace_section" => JsonSerializer.Deserialize<ReplaceSectionOperation>(json, options),
            "search_replace" => JsonSerializer.Deserialize<SearchReplaceOperation>(json, options),
            "append_section" => JsonSerializer.Deserialize<AppendSectionOperation>(json, options),
            "insert_after_block" => JsonSerializer.Deserialize<InsertAfterBlockOperation>(json, options),
            _ => throw new JsonException($"Unknown patch operation: '{op}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, PatchOperation value, JsonSerializerOptions options)
    {
        var op = value switch
        {
            ReplaceBlockOperation => "replace_block",
            ReplaceSectionOperation => "replace_section",
            SearchReplaceOperation => "search_replace",
            AppendSectionOperation => "append_section",
            InsertAfterBlockOperation => "insert_after_block",
            _ => throw new JsonException($"Unknown patch operation type: '{value.GetType().Name}'.")
        };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value, value.GetType(), options));

        writer.WriteStartObject();
        writer.WriteString(Discriminator, op);

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
