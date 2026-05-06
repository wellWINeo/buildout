namespace Buildout.Core.Search;

public interface ISearchService
{
    Task<IReadOnlyList<SearchMatch>> SearchAsync(string query, string? pageId, CancellationToken cancellationToken = default);
}
