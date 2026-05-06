using System.Text;

namespace Buildout.Core.Search;

public sealed class SearchResultFormatter : ISearchResultFormatter
{
    public string Format(IReadOnlyList<SearchMatch> matches)
    {
        if (matches.Count == 0)
            return string.Empty;

        var sb = new StringBuilder(matches.Count * 80);

        for (int i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            sb.Append(m.PageId);
            sb.Append('\t');
            sb.Append(m.ObjectType.ToString().ToLowerInvariant());
            sb.Append('\t');
            sb.Append(m.DisplayTitle);
            sb.Append('\n');
        }

        return sb.ToString();
    }
}
