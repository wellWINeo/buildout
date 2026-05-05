using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Conversion;

public interface IInlineRenderer
{
    string Render(IReadOnlyList<RichText>? items, int indentLevel);
}
