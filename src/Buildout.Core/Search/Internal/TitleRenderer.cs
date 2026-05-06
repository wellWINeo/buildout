using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Search.Internal;

internal interface ITitleRenderer
{
    string RenderPlain(IReadOnlyList<RichText>? title);
}

internal sealed class TitleRenderer : ITitleRenderer
{
    public string RenderPlain(IReadOnlyList<RichText>? title)
    {
        if (title is null or { Count: 0 })
            return "(untitled)";

        var result = string.Concat(title.Select(t => t.Content))
            .Replace('\t', ' ');

        if (string.IsNullOrWhiteSpace(result))
            return "(untitled)";

        return result;
    }
}
