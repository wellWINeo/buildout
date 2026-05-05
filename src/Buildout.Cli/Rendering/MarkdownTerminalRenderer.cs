using System.Text;
using Markdig;
using Markdig.Syntax;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Buildout.Cli.Rendering;

public sealed class MarkdownTerminalRenderer
{
    private readonly IAnsiConsole _console;

    public MarkdownTerminalRenderer(IAnsiConsole console)
    {
        _console = console;
    }

    public void Render(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        var doc = Markdig.Markdown.Parse(markdown, pipeline);

        foreach (var block in doc)
        {
            RenderBlock(block);
        }
    }

    private void RenderBlock(MarkdownObject block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                RenderHeading(heading);
                break;
            case ParagraphBlock paragraph:
                RenderParagraph(paragraph);
                break;
            case ListBlock list:
                RenderList(list);
                break;
            case FencedCodeBlock code:
                RenderFencedCode(code);
                break;
            case QuoteBlock quote:
                RenderQuote(quote);
                break;
            case ThematicBreakBlock:
                _console.Write(new Rule());
                _console.WriteLine();
                break;
            case HtmlBlock html:
                RenderHtmlBlock(html);
                break;
            default:
                break;
        }
    }

    private void RenderHeading(HeadingBlock heading)
    {
        var text = heading.Inline?.FirstChild;
        if (text is null) return;

        var content = GetInlineText(heading);
        if (string.IsNullOrEmpty(content)) return;

        var style = heading.Level switch
        {
            1 => new Style(foreground: Color.Blue, decoration: Decoration.Bold),
            2 => new Style(foreground: Color.Cyan, decoration: Decoration.Bold),
            _ => new Style(decoration: Decoration.Bold)
        };

        _console.Write(new Text(content, style));
        _console.WriteLine();

        if (heading.Level == 1)
        {
            _console.Write(new Rule());
        }

        _console.WriteLine();
    }

    private void RenderParagraph(ParagraphBlock paragraph)
    {
        var segments = BuildInlineSegments(paragraph);
        foreach (var segment in segments)
        {
            _console.Write(segment);
        }
        _console.WriteLine();
        _console.WriteLine();
    }

    private void RenderList(ListBlock list)
    {
        var index = 0;
        foreach (var item in list)
        {
            if (item is ListItemBlock listItem)
            {
                index++;
                var prefix = list.IsOrdered
                    ? $"{index}. "
                    : "- ";

                foreach (var subBlock in listItem)
                {
                    if (subBlock is ParagraphBlock p)
                    {
                        var content = GetInlineText(p);
                        var text = prefix + content;
                        _console.Write(new Text(text));
                        _console.WriteLine();
                    }
                    else if (subBlock is ListBlock nestedList)
                    {
                        RenderList(nestedList);
                    }
                }
            }
        }
        _console.WriteLine();
    }

    private void RenderFencedCode(FencedCodeBlock code)
    {
        var language = code.Info ?? string.Empty;
        var lines = code.Lines;

        var codeText = new StringBuilder();
        foreach (var line in lines)
        {
            codeText.AppendLine(line.ToString());
        }

        var panel = new Panel(codeText.ToString().TrimEnd('\n', '\r'))
            .RoundedBorder();

        if (!string.IsNullOrEmpty(language))
        {
            panel.Header = new PanelHeader(language);
        }

        _console.Write(panel);
        _console.WriteLine();
    }

    private void RenderQuote(QuoteBlock quote)
    {
        foreach (var block in quote)
        {
            if (block is ParagraphBlock p)
            {
                var content = GetInlineText(p);
                var styledText = new Text(content, new Style(decoration: Decoration.Italic));
                var panel = new Panel(styledText)
                    .NoBorder()
                    .PadLeft(2);
                _console.Write(panel);
                _console.WriteLine();
            }
        }
        _console.WriteLine();
    }

    private void RenderHtmlBlock(HtmlBlock html)
    {
        var lines = html.Lines;
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            sb.AppendLine(line.ToString());
        }

        _console.Write(new Text(sb.ToString().TrimEnd('\n', '\r'),
            new Style(foreground: Color.Grey)));
        _console.WriteLine();
    }

    private static string GetInlineText(LeafBlock leaf)
    {
        if (leaf.Inline is null) return string.Empty;

        var sb = new StringBuilder();
        var inline = leaf.Inline.FirstChild;
        while (inline is not null)
        {
            sb.Append(inline);
            inline = inline.NextSibling;
        }

        return sb.ToString();
    }

    private static List<IRenderable> BuildInlineSegments(ParagraphBlock paragraph)
    {
        var segments = new List<IRenderable>();
        if (paragraph.Inline is null) return segments;

        var inline = paragraph.Inline.FirstChild;
        while (inline is not null)
        {
            switch (inline)
            {
                case Markdig.Syntax.Inlines.LiteralInline literal:
                    segments.Add(new Text(literal.Content.ToString()));
                    break;
                case Markdig.Syntax.Inlines.EmphasisInline emphasis:
                    var text = emphasis.ContentToString();
                    var style = emphasis.DelimiterCount >= 2
                        ? new Style(decoration: Decoration.Bold)
                        : new Style(decoration: Decoration.Italic);
                    segments.Add(new Text(text, style));
                    break;
                case Markdig.Syntax.Inlines.CodeInline code:
                    segments.Add(new Text(code.Content, new Style(foreground: Color.Green)));
                    break;
                case Markdig.Syntax.Inlines.LinkInline link:
                    var linkText = link.FirstChild?.ToString() ?? link.Url ?? string.Empty;
                    segments.Add(new Text(linkText, new Style(foreground: Color.Blue, decoration: Decoration.Underline)));
                    break;
                default:
                    var fallback = inline.ToString();
                    if (!string.IsNullOrEmpty(fallback))
                        segments.Add(new Text(fallback));
                    break;
            }
            inline = inline.NextSibling;
        }

        return segments;
    }
}

file static class EmphasisInlineExtensions
{
    public static string ContentToString(this Markdig.Syntax.Inlines.ContainerInline container)
    {
        var sb = new StringBuilder();
        var child = container.FirstChild;
        while (child is not null)
        {
            sb.Append(child);
            child = child.NextSibling;
        }
        return sb.ToString();
    }
}
