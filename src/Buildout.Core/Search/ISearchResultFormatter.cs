namespace Buildout.Core.Search;

public interface ISearchResultFormatter
{
    string Format(IReadOnlyList<SearchMatch> matches);
}
