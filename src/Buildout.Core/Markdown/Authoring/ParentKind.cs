using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Authoring;

public abstract record ParentKind
{
    private ParentKind() { }

    public sealed record Page(string PageId) : ParentKind;
    public sealed record DatabaseParent(Database Schema) : ParentKind;
    public sealed record NotFound : ParentKind;
}
