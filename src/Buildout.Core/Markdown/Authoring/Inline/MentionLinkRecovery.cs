using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Authoring.Inline;

public sealed class MentionLinkRecovery
{
    public static IReadOnlyList<RichText> Recover(IReadOnlyList<RichText> richTexts)
    {
        var results = new List<RichText>(richTexts.Count);
        foreach (var rt in richTexts)
        {
            if (rt.Href is not null && rt.Href.StartsWith("buildin://", StringComparison.Ordinal))
            {
                var id = rt.Href.Substring("buildin://".Length);
                results.Add(new RichText
                {
                    Type = "mention",
                    Content = rt.Content,
                    Href = null,
                    Annotations = rt.Annotations,
                    Mention = new PageMention { PageId = id }
                });
            }
            else
            {
                results.Add(rt);
            }
        }
        return results;
    }
}
