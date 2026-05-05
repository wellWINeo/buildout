using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Conversion.Mentions;

internal sealed class PageMentionConverter : IMentionToMarkdownConverter
{
    public Type MentionClrType => typeof(PageMention);
    public string MentionType => "page";

    public string Render(Mention mention, string displayText)
    {
        var page = (PageMention)mention;
        return $"[{displayText}](buildin://{page.PageId})";
    }
}
