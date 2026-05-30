using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.PageTree.Errors;
using Microsoft.Extensions.Logging;

namespace Buildout.Core.PageTree;

public sealed class PageTreeService : IPageTreeService
{
    private readonly IBuildinClient _client;
    private readonly ILogger<PageTreeService> _logger;

    public PageTreeService(IBuildinClient client, ILogger<PageTreeService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<TreeNode> BuildAsync(string targetId, int depth, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException("targetId must not be empty.", nameof(targetId));

        TreeDepth.Validate(depth);

        var visited = new HashSet<string>(StringComparer.Ordinal) { targetId };

        Page? page = null;
        Database? database = null;
        BuildinApiException? lastException = null;

        try
        {
            page = await _client.GetPageAsync(targetId, cancellationToken).ConfigureAwait(false);
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
        {
            lastException = ex;
        }

        if (page is null)
        {
            try
            {
                database = await _client.GetDatabaseAsync(targetId, cancellationToken).ConfigureAwait(false);
            }
            catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
            {
                throw new TreeRootNotFoundException(targetId, lastException ?? ex);
            }
        }

        var rootName = page is not null
            ? RenderTitle(page.Title)
            : RenderTitle(database!.Title);
        var rootUrl = page?.Url ?? database!.Url ?? string.Empty;

        var children = depth > 0
            ? await GetChildrenAsync(targetId, isDatabase: database is not null, remainingDepth: depth - 1, visited, cancellationToken).ConfigureAwait(false)
            : Array.Empty<TreeNode>();

        return new TreeNode(rootName, rootUrl, children);
    }

    private async Task<IReadOnlyList<TreeNode>> GetChildrenAsync(
        string nodeId,
        bool isDatabase,
        int remainingDepth,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        if (isDatabase)
            return await GetDatabaseChildrenAsync(nodeId, remainingDepth, visited, cancellationToken).ConfigureAwait(false);
        else
            return await GetPageChildrenAsync(nodeId, remainingDepth, visited, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<TreeNode>> GetPageChildrenAsync(
        string pageId,
        int remainingDepth,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        var children = new List<TreeNode>();
        string? cursor = null;

        do
        {
            PaginatedList<Block> page;
            try
            {
                var query = cursor is not null ? new BlockChildrenQuery { StartCursor = cursor } : null;
                page = await _client.GetBlockChildrenAsync(pageId, query, cancellationToken).ConfigureAwait(false);
            }
            catch (BuildinApiException ex)
            {
                PageTreeServiceLog.FailedBlockChildren(_logger, pageId, ex);
                return children;
            }

            foreach (var block in page.Results)
            {
                TreeNode? child = block switch
                {
                    ChildPageBlock cpb => await BuildChildPageNodeAsync(cpb.Id, remainingDepth, visited, cancellationToken).ConfigureAwait(false),
                    ChildDatabaseBlock cdb => await BuildChildDatabaseNodeAsync(cdb.Id, remainingDepth, visited, cancellationToken).ConfigureAwait(false),
                    _ => null
                };

                if (child is not null)
                    children.Add(child);
            }

            cursor = page.HasMore ? page.NextCursor : null;
        }
        while (cursor is not null);

        return children;
    }

    private async Task<IReadOnlyList<TreeNode>> GetDatabaseChildrenAsync(
        string databaseId,
        int remainingDepth,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        var children = new List<TreeNode>();
        string? cursor = null;

        do
        {
            QueryDatabaseResult result;
            try
            {
                result = await _client.QueryDatabaseAsync(databaseId, new QueryDatabaseRequest { StartCursor = cursor }, cancellationToken).ConfigureAwait(false);
            }
            catch (BuildinApiException ex)
            {
                PageTreeServiceLog.FailedDatabaseQuery(_logger, databaseId, ex);
                return children;
            }

            foreach (var pageRef in result.Pages)
            {
                if (string.IsNullOrEmpty(pageRef.Id))
                    continue;

                if (visited.Contains(pageRef.Id))
                    throw new TreeCycleDetectedException(pageRef.Id);
                visited.Add(pageRef.Id);

                var name = string.IsNullOrWhiteSpace(pageRef.Title) ? "(untitled)" : pageRef.Title;
                var url = pageRef.Url ?? string.Empty;

                var grandChildren = remainingDepth > 0
                    ? await GetChildrenAsync(pageRef.Id, isDatabase: false, remainingDepth - 1, visited, cancellationToken).ConfigureAwait(false)
                    : Array.Empty<TreeNode>();

                children.Add(new TreeNode(name, url, grandChildren));
            }

            cursor = result.HasMore ? result.NextCursor : null;
        }
        while (cursor is not null);

        return children;
    }

    private async Task<TreeNode> BuildChildPageNodeAsync(
        string pageId,
        int remainingDepth,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        if (visited.Contains(pageId))
            throw new TreeCycleDetectedException(pageId);
        visited.Add(pageId);

        Page page;
        try
        {
            page = await _client.GetPageAsync(pageId, cancellationToken).ConfigureAwait(false);
        }
        catch (BuildinApiException ex)
        {
            PageTreeServiceLog.FailedChildPage(_logger, pageId, ex);
            return new TreeNode("(unavailable)", string.Empty, Array.Empty<TreeNode>());
        }

        var name = RenderTitle(page.Title);
        var url = page.Url ?? string.Empty;

        var children = remainingDepth > 0
            ? await GetChildrenAsync(pageId, isDatabase: false, remainingDepth - 1, visited, cancellationToken).ConfigureAwait(false)
            : Array.Empty<TreeNode>();

        return new TreeNode(name, url, children);
    }

    private async Task<TreeNode> BuildChildDatabaseNodeAsync(
        string databaseId,
        int remainingDepth,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        if (visited.Contains(databaseId))
            throw new TreeCycleDetectedException(databaseId);
        visited.Add(databaseId);

        Database db;
        try
        {
            db = await _client.GetDatabaseAsync(databaseId, cancellationToken).ConfigureAwait(false);
        }
        catch (BuildinApiException ex)
        {
            PageTreeServiceLog.FailedChildDatabase(_logger, databaseId, ex);
            return new TreeNode("(unavailable)", string.Empty, Array.Empty<TreeNode>());
        }

        var name = RenderTitle(db.Title);
        var url = db.Url ?? string.Empty;

        var children = remainingDepth > 0
            ? await GetChildrenAsync(databaseId, isDatabase: true, remainingDepth - 1, visited, cancellationToken).ConfigureAwait(false)
            : Array.Empty<TreeNode>();

        return new TreeNode(name, url, children);
    }

    private static string RenderTitle(IReadOnlyList<RichText>? title)
    {
        if (title is null or { Count: 0 })
            return "(untitled)";

        var result = string.Concat(title.Select(t => t.Content)).Replace('\t', ' ');
        return string.IsNullOrWhiteSpace(result) ? "(untitled)" : result;
    }
}
