namespace Buildout.Core.DatabaseViews;

public sealed class DatabaseViewValidationException : ArgumentException
{
    public string OffendingField { get; }
    public IReadOnlyList<string> ValidAlternatives { get; }

    public DatabaseViewValidationException(string message, string offendingField, IReadOnlyList<string>? validAlternatives = null)
        : base(message)
    {
        OffendingField = offendingField;
        ValidAlternatives = validAlternatives ?? [];
    }
}
