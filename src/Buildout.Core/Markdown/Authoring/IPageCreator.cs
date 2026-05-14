namespace Buildout.Core.Markdown.Authoring;

public interface IPageCreator
{
    Task<CreatePageOutcome> CreateAsync(CreatePageInput input, CancellationToken cancellationToken = default);
}
