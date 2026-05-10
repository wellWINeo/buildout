namespace Buildout.Core.DatabaseViews.Rendering;

public sealed record CellBudget
{
    public int MaxCharacters { get; init; }
    public string EllipsisMarker { get; init; }

    public CellBudget(int maxCharacters, string ellipsisMarker)
    {
        MaxCharacters = maxCharacters;
        EllipsisMarker = ellipsisMarker;
    }

    public string Truncate(string value)
    {
        if (value.Length <= MaxCharacters)
            return value;

        var contentLength = MaxCharacters - EllipsisMarker.Length;
        return string.Concat(value.AsSpan(0, contentLength), EllipsisMarker);
    }
}
