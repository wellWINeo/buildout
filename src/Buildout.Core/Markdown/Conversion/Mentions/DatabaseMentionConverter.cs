using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Conversion.Mentions;

internal sealed class DatabaseMentionConverter : IMentionToMarkdownConverter
{
    public Type MentionClrType => typeof(DatabaseMention);
    public string MentionType => "database";

    public string Render(Mention mention, string displayText)
    {
        var db = (DatabaseMention)mention;
        return $"[{displayText}](buildin://{db.DatabaseId})";
    }
}
