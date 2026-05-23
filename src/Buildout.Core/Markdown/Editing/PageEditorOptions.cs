namespace Buildout.Core.Markdown.Editing;

[Obsolete("Use LimitationsOptions. PageEditorOptions will be removed in a future release.")]
public sealed class PageEditorOptions
{
    public int LargeDeleteThreshold { get; set; } = 10;
}
