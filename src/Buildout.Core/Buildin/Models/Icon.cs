namespace Buildout.Core.Buildin.Models;

public abstract record Icon
{
    private protected Icon(string type) => Type = type;
    public string Type { get; }
}

public sealed record IconEmoji(string Emoji) : Icon("emoji");

public sealed record IconExternal(string Url) : Icon("external");

public sealed record IconFile(string Url) : Icon("file");
