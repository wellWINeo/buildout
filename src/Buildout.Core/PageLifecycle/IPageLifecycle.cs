namespace Buildout.Core.PageLifecycle;

public interface IPageLifecycle
{
    Task<PageLifecycleOutcome> DeleteAsync(string pageId, CancellationToken cancellationToken = default);

    Task<PageLifecycleOutcome> RestoreAsync(string pageId, CancellationToken cancellationToken = default);
}
