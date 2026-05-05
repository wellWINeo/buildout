using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Conversion.Mentions;

internal sealed class DateMentionConverter : IMentionToMarkdownConverter
{
    public Type MentionClrType => typeof(DateMention);
    public string MentionType => "date";

    public string Render(Mention mention, string displayText)
    {
        var date = (DateMention)mention;

        if (string.IsNullOrEmpty(date.Start))
            return displayText;

        if (!string.IsNullOrEmpty(date.End))
            return $"{date.Start} \u2013 {date.End}";

        return date.Start;
    }
}
