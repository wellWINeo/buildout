using System.Runtime.Serialization;
using System.Text.Json;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;

namespace Buildout.Core.Buildin.Mapping;

internal static class MappingHelpers
{
    public static JsonElement SerializeToElement(IParsable kiotaModel)
    {
        using var writer = new JsonSerializationWriter();
        writer.WriteObjectValue(null, kiotaModel);
        using var stream = writer.GetSerializedContent();
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.Clone();
    }

    public static string GetEnumValue<T>(T? value) where T : struct, Enum
    {
        if (value is null) return "unsupported";
        var name = value.Value.ToString();
        var field = typeof(T).GetField(name);
        if (field is null) return name;
        var attr = field.GetCustomAttributes(typeof(EnumMemberAttribute), false)
            .Cast<EnumMemberAttribute>()
            .FirstOrDefault();
        return attr?.Value ?? name;
    }
}
