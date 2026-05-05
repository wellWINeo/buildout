namespace Buildout.Core.Buildin.Models;

public sealed record Annotations
{
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public bool Strikethrough { get; init; }
    public bool Underline { get; init; }
    public bool Code { get; init; }
    public string Color { get; init; } = "default";
}
