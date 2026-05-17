namespace Buildout.Core.Markdown.Editing;

public abstract class PatchRejectedException : Exception
{
    public string PatchErrorClass { get; }
    public IReadOnlyDictionary<string, object>? Details { get; }

    protected PatchRejectedException(
        string patchErrorClass,
        string message,
        IReadOnlyDictionary<string, object>? details = null)
        : base(message)
    {
        PatchErrorClass = patchErrorClass;
        Details = details;
    }

    protected PatchRejectedException(
        string patchErrorClass,
        string message,
        Exception innerException,
        IReadOnlyDictionary<string, object>? details = null)
        : base(message, innerException)
    {
        PatchErrorClass = patchErrorClass;
        Details = details;
    }
}
