namespace Buildout.Core.Caching;

/// <summary>
/// Implementation of <see cref="IPageContentProvider"/> that always fetches via the base provider.
/// </summary>
public sealed class PassthroughPageContentProvider : IPageContentProvider
{
    private readonly Func<string, CancellationToken, Task<PageContent>> _fetchDelegate;

    public PassthroughPageContentProvider(Func<string, CancellationToken, Task<PageContent>> fetchDelegate)
    {
        _fetchDelegate = fetchDelegate;
    }

    /// <inheritdoc />
    public Task<PageContent> FetchAsync(string pageId, CancellationToken ct)
    {
        return _fetchDelegate(pageId, ct);
    }
}