using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Conversion;

public interface IMentionToMarkdownConverter
{
    Type MentionClrType { get; }
    string MentionType { get; }
    string Render(Mention mention, string displayText);
}
