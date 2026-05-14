using System.Globalization;
using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Authoring.Properties;

public sealed class DatabasePropertyValueParser : IDatabasePropertyValueParser
{
    public PropertyValue Parse(string name, string raw, PropertySchema schema)
    {
        return schema switch
        {
            TitlePropertySchema => new TitlePropertyValue
            {
                Title = [new RichText { Type = "text", Content = raw }]
            },
            RichTextPropertySchema => new RichTextPropertyValue
            {
                RichText = [new RichText { Type = "text", Content = raw }]
            },
            NumberPropertySchema => ParseNumber(raw),
            SelectPropertySchema select => ParseSelect(raw, select),
            MultiSelectPropertySchema multi => ParseMultiSelect(raw, multi),
            CheckboxPropertySchema => ParseCheckbox(raw),
            DatePropertySchema => ParseDate(raw),
            UrlPropertySchema => new UrlPropertyValue { Url = raw },
            EmailPropertySchema => new RichTextPropertyValue
            {
                RichText = [new RichText { Type = "text", Content = raw }]
            },
            PhonePropertySchema => new RichTextPropertyValue
            {
                RichText = [new RichText { Type = "text", Content = raw }]
            },
            _ => throw new ArgumentException($"Property '{name}' is of kind '{schema.Type}', which is not supported in v1.")
        };
    }

    private static NumberPropertyValue ParseNumber(string raw)
    {
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            throw new ArgumentException($"Cannot parse '{raw}' as a number.");
        return new NumberPropertyValue { Number = number };
    }

    private static SelectPropertyValue ParseSelect(string raw, SelectPropertySchema schema)
    {
        var option = schema.Options?.FirstOrDefault(o => o.Name == raw);
        if (option is null)
            throw new ArgumentException($"Unknown select option '{raw}'. Valid options: {string.Join(", ", schema.Options?.Select(o => o.Name) ?? [])}");
        return new SelectPropertyValue { Select = option };
    }

    private static MultiSelectPropertyValue ParseMultiSelect(string raw, MultiSelectPropertySchema schema)
    {
        var tokens = raw.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0);
        var options = new List<SelectOption>();
        foreach (var token in tokens)
        {
            var option = schema.Options?.FirstOrDefault(o => o.Name == token);
            if (option is null)
                throw new ArgumentException($"Unknown multi_select option '{token}'. Valid options: {string.Join(", ", schema.Options?.Select(o => o.Name) ?? [])}");
            options.Add(option);
        }
        return new MultiSelectPropertyValue { MultiSelect = options };
    }

    private static CheckboxPropertyValue ParseCheckbox(string raw)
    {
        return raw.ToLowerInvariant() switch
        {
            "true" or "yes" => new CheckboxPropertyValue { Checkbox = true },
            "false" or "no" => new CheckboxPropertyValue { Checkbox = false },
            _ => throw new ArgumentException($"Cannot parse '{raw}' as a boolean. Use true/false/yes/no.")
        };
    }

    private static DatePropertyValue ParseDate(string raw)
    {
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
        {
            return new DatePropertyValue { Date = new DateRange { Start = dto.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) } };
        }
        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, out var dateOnly))
        {
            return new DatePropertyValue { Date = new DateRange { Start = dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) } };
        }
        throw new ArgumentException($"Cannot parse '{raw}' as a date.");
    }
}
