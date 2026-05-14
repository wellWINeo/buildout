using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Authoring;

public sealed class ParentKindProbe
{
    private readonly IBuildinClient _client;

    public ParentKindProbe(IBuildinClient client)
    {
        _client = client;
    }

    public async Task<ParentKind> ProbeAsync(string parentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await _client.GetPageAsync(parentId, cancellationToken);
            return new ParentKind.Page(page.Id);
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
        {
        }

        try
        {
            var database = await _client.GetDatabaseAsync(parentId, cancellationToken);
            return new ParentKind.DatabaseParent(database);
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
        {
        }

        return new ParentKind.NotFound();
    }
}
