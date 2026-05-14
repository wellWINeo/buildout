using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Authoring.Inline;

public interface IInlineMarkdownParser
{
    IReadOnlyList<RichText> ParseInlines(Markdig.Syntax.Inlines.ContainerInline container);
}
