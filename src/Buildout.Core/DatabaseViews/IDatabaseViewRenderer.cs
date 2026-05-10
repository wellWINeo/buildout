namespace Buildout.Core.DatabaseViews;

public interface IDatabaseViewRenderer
{
    Task<string> RenderAsync(DatabaseViewRequest request, CancellationToken cancellationToken = default);
    Task<string> RenderInlineAsync(string databaseId, CancellationToken cancellationToken = default);
}
