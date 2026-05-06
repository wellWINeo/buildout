using System.Diagnostics.CodeAnalysis;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Microsoft.Extensions.Logging;

namespace Buildout.Core.Search.Internal;

[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Internal class with infrequent debug logging")]
internal sealed class AncestorScopeFilter
{
    private readonly IBuildinClient _client;
    private readonly ILogger _logger;

    public AncestorScopeFilter(IBuildinClient client, ILogger<AncestorScopeFilter> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async ValueTask<bool> IsInScopeAsync(
        SearchMatch match,
        string scopePageId,
        Dictionary<string, Parent?> parentLookup,
        CancellationToken ct)
    {
        if (match.PageId == scopePageId)
            return true;

        var visited = new HashSet<string> { match.PageId };
        var currentParent = match.Parent;

        while (currentParent is ParentPage or ParentBlock)
        {
            var ancestorId = currentParent switch
            {
                ParentPage p => p.Id,
                ParentBlock b => b.Id,
                _ => null
            };

            if (ancestorId is null)
                return false;

            if (ancestorId == scopePageId)
                return true;

            if (!visited.Add(ancestorId))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Cycle detected in parent chain at {AncestorId}", ancestorId);
                return false;
            }

            if (parentLookup.TryGetValue(ancestorId, out var nextParent))
            {
                currentParent = nextParent;
            }
            else
            {
                try
                {
                    var page = await _client.GetPageAsync(ancestorId, ct);
                    nextParent = page.Parent;
                }
                catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 or 403 })
                {
                    parentLookup[ancestorId] = null;
                    return false;
                }

                parentLookup[ancestorId] = nextParent;
                currentParent = nextParent;
            }
        }

        return false;
    }
}
