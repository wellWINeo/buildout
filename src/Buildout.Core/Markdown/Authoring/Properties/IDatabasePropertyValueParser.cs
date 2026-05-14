using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Authoring.Properties;

public interface IDatabasePropertyValueParser
{
    PropertyValue Parse(string name, string raw, PropertySchema schema);
}
