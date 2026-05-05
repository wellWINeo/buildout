namespace Buildout.Core.Buildin.Models;

public abstract record Parent
{
    private protected Parent(string type) => Type = type;
    public string Type { get; }
}

public sealed record ParentDatabase(string Id) : Parent("database_id");

public sealed record ParentPage(string Id) : Parent("page_id");

public sealed record ParentBlock(string Id) : Parent("block_id");

public sealed record ParentWorkspace(string? WorkspaceType) : Parent("workspace");
