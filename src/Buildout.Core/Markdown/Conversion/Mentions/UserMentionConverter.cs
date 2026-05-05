using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Conversion.Mentions;

internal sealed class UserMentionConverter : IMentionToMarkdownConverter
{
    public Type MentionClrType => typeof(UserMention);
    public string MentionType => "user";

    public string Render(Mention mention, string displayText)
    {
        var user = (UserMention)mention;
        var name = !string.IsNullOrEmpty(user.DisplayName) ? user.DisplayName : displayText;
        return $"@{name}";
    }
}
