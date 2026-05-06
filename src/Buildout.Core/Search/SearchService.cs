using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Search.Internal;
using Microsoft.Extensions.Logging;

namespace Buildout.Core.Search;

internal sealed class SearchService : ISearchService
{
    private readonly IBuildinClient _client;
    private readonly ITitleRenderer _titleRenderer;
    private readonly AncestorScopeFilter _scopeFilter;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        IBuildinClient client,
        ITitleRenderer titleRenderer,
        AncestorScopeFilter scopeFilter,
        ILogger<SearchService> logger)
    {
        _client = client;
        _titleRenderer = titleRenderer;
        _scopeFilter = scopeFilter;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SearchMatch>> SearchAsync(
        string query,
        string? pageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query must be non-empty.", nameof(query));

        var allPages = new List<Page>();
        string? cursor = null;

        do
        {
            var request = new PageSearchRequest { Query = query, StartCursor = cursor };
            var response = await _client.SearchPagesAsync(request, cancellationToken);

            if (response.Results is not null)
                allPages.AddRange(response.Results);

            cursor = response.HasMore ? response.NextCursor : null;
        } while (cursor is not null);

        var matches = allPages
            .Where(p => !p.Archived)
            .Select(p => new SearchMatch
            {
                PageId = p.Id,
                ObjectType = MapObjectType(p.ObjectType),
                DisplayTitle = _titleRenderer.RenderPlain(p.Title),
                Parent = p.Parent,
                Archived = p.Archived
            })
            .ToList();

        if (pageId is not null)
        {
            var parentLookup = matches.ToDictionary(m => m.PageId, m => m.Parent);
            var filtered = new List<SearchMatch>();
            foreach (var match in matches)
            {
                if (await _scopeFilter.IsInScopeAsync(match, pageId, parentLookup, cancellationToken))
                    filtered.Add(match);
            }
            matches = filtered;
        }

        return matches;
    }

    private static SearchObjectType MapObjectType(string? objectType) => objectType switch
    {
        "page" => SearchObjectType.Page,
        "database" => SearchObjectType.Database,
        _ => SearchObjectType.Page
    };
}
