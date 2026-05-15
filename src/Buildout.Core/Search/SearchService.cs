using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Diagnostics;
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

    [SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Dynamic operation names prevent compile-time LoggerMessage definitions")]
    [SuppressMessage("Performance", "CA1873:Evaluate log message arguments eagerly", Justification = "All arguments are cheap string variables")]
    public async Task<IReadOnlyList<SearchMatch>> SearchAsync(
        string query,
        string? pageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query must be non-empty.", nameof(query));

        using var recorder = OperationRecorder.Start(_logger, "search");
        try
        {
            var allPages = new List<Page>();
            string? cursor = null;
            int pageNumber = 1;

            do
            {
                var request = new PageSearchRequest { Query = query, StartCursor = cursor };
                var response = await _client.SearchPagesAsync(request, cancellationToken);

                if (response.Results is not null)
                    allPages.AddRange(response.Results);

                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("SearchAsync pagination_page={PageNumber} pagination_items={ItemCount}",
                        pageNumber, response.Results?.Count ?? 0);
                pageNumber++;

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

            recorder.SetTag("query", query.Length > 100 ? query[..100] + "…" : query);
            recorder.SetTag("result_count", matches.Count);
            BuildoutMeter.SearchResultsTotal.Add(matches.Count, new TagList { { "operation", "search" } });
            recorder.Succeed();
            return matches;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            recorder.Fail("unknown");
            throw;
        }
    }

    private static SearchObjectType MapObjectType(string? objectType) => objectType switch
    {
        "page" => SearchObjectType.Page,
        "database" => SearchObjectType.Database,
        _ => SearchObjectType.Page
    };
}
