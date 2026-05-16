namespace Buildout.Core.Markdown.Editing;

public interface IPageEditor
{
    Task<AnchoredPageSnapshot> FetchForEditAsync(
        string pageId,
        CancellationToken cancellationToken = default);

    Task<ReconciliationSummary> UpdateAsync(
        UpdatePageInput input,
        CancellationToken cancellationToken = default);
}
