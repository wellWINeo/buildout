namespace Buildout.Core.Markdown;

public interface IPageMarkdownRenderer
{
    Task<string> RenderAsync(
        string pageId,
        CancellationToken cancellationToken = default);
}
