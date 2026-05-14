using System.Text;
using Buildout.Core.Buildin.Models;
using Markdig.Syntax.Inlines;

namespace Buildout.Core.Markdown.Authoring.Inline;

public sealed class InlineMarkdownParser : IInlineMarkdownParser
{
    public IReadOnlyList<RichText> ParseInlines(ContainerInline container)
    {
        var results = new List<RichText>();
        foreach (var inline in container)
        {
            WalkInline(inline, results, null);
        }
        return results;
    }

    private static void WalkInline(Markdig.Syntax.Inlines.Inline inline, List<RichText> results, Annotations? parentAnnotations)
    {
        switch (inline)
        {
            case LiteralInline literal:
                AddText(results, literal.Content.ToString(), parentAnnotations);
                break;
            case CodeInline code:
                AddText(results, code.Content.ToString(), new Annotations { Code = true });
                break;
            case EmphasisInline emphasis:
                var emphasisAnnotations = emphasis.DelimiterCount >= 2
                    ? new Annotations { Bold = true }
                    : new Annotations { Italic = true };
                foreach (var child in emphasis)
                {
                    WalkInline(child, results, CombineAnnotations(parentAnnotations, emphasisAnnotations));
                }
                break;
            case LinkInline link:
                if (link.IsImage) break;
                var linkText = ExtractLinkText(link);
                if (!string.IsNullOrEmpty(link.Url) && link.Url.StartsWith("buildin://", StringComparison.Ordinal))
                {
                    var id = link.Url.Substring("buildin://".Length);
                    results.Add(new RichText
                    {
                        Type = "mention",
                        Content = linkText,
                        Mention = new PageMention { PageId = id }
                    });
                }
                else if (!string.IsNullOrEmpty(link.Url))
                {
                    results.Add(new RichText
                    {
                        Type = "text",
                        Content = linkText,
                        Href = link.Url,
                        Annotations = parentAnnotations
                    });
                }
                else
                {
                    AddText(results, linkText, parentAnnotations);
                }
                break;
            case LineBreakInline lineBreak:
                if (lineBreak.IsHard)
                {
                    AddText(results, "\n", parentAnnotations);
                }
                break;
        }
    }

    private static string ExtractLinkText(LinkInline link)
    {
        var sb = new StringBuilder();
        foreach (var child in link)
        {
            if (child is LiteralInline literal)
                sb.Append(literal.Content.ToString());
        }
        return sb.ToString();
    }

    private static void AddText(List<RichText> results, string content, Annotations? annotations)
    {
        results.Add(new RichText
        {
            Type = "text",
            Content = content,
            Annotations = annotations
        });
    }

    private static Annotations? CombineAnnotations(Annotations? parent, Annotations? child)
    {
        if (parent is null) return child;
        if (child is null) return parent;
        return new Annotations
        {
            Bold = parent.Bold || child.Bold,
            Italic = parent.Italic || child.Italic,
            Code = parent.Code || child.Code,
            Strikethrough = parent.Strikethrough || child.Strikethrough,
            Underline = parent.Underline || child.Underline,
            Color = parent.Color != "default" ? parent.Color : child.Color
        };
    }
}
