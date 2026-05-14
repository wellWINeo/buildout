using Markdig.Syntax;
using Buildout.Core.Markdown.Authoring.Inline;

namespace Buildout.Core.Markdown.Authoring;

public sealed class TitleExtractor
{
    public static (string? Title, MarkdownDocument Document) Extract(MarkdownDocument document, IInlineMarkdownParser inlineParser)
    {
        Markdig.Syntax.Block? firstHeading = null;
        int firstHeadingIndex = -1;

        for (int i = 0; i < document.Count; i++)
        {
            if (document[i] is HeadingBlock heading && heading.Level == 1)
            {
                firstHeading = heading;
                firstHeadingIndex = i;
                break;
            }
            if (document[i] is not LinkReferenceDefinitionGroup)
            {
                break;
            }
        }

        if (firstHeading is Markdig.Syntax.HeadingBlock h1 && firstHeadingIndex >= 0)
        {
            string? title = null;
            if (h1.Inline is not null)
            {
                var richTexts = inlineParser.ParseInlines(h1.Inline);
                title = string.Join("", richTexts.Select(r => r.Content));
            }

            document.RemoveAt(firstHeadingIndex);
            return (title?.Trim(), document);
        }

        return (null, document);
    }
}
