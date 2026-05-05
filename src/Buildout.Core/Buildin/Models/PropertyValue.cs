namespace Buildout.Core.Buildin.Models;

public abstract record PropertyValue
{
    public string Type { get; }
    public string? Id { get; init; }

    private protected PropertyValue(string type) => Type = type;
}

public sealed record TitlePropertyValue : PropertyValue
{
    public IReadOnlyList<RichText>? Title { get; init; }
    public TitlePropertyValue() : base("title") { }
}

public sealed record RichTextPropertyValue : PropertyValue
{
    public IReadOnlyList<RichText>? RichText { get; init; }
    public RichTextPropertyValue() : base("rich_text") { }
}

public sealed record NumberPropertyValue : PropertyValue
{
    public double? Number { get; init; }
    public NumberPropertyValue() : base("number") { }
}

public sealed record SelectPropertyValue : PropertyValue
{
    public SelectOption? Select { get; init; }
    public SelectPropertyValue() : base("select") { }
}

public sealed record MultiSelectPropertyValue : PropertyValue
{
    public IReadOnlyList<SelectOption>? MultiSelect { get; init; }
    public MultiSelectPropertyValue() : base("multi_select") { }
}

public sealed record DatePropertyValue : PropertyValue
{
    public DateRange? Date { get; init; }
    public DatePropertyValue() : base("date") { }
}

public sealed record FormulaPropertyValue : PropertyValue
{
    public string? StringResult { get; init; }
    public double? NumberResult { get; init; }
    public bool? BooleanResult { get; init; }
    public DateRange? DateResult { get; init; }
    public FormulaPropertyValue() : base("formula") { }
}

public sealed record RelationPropertyValue : PropertyValue
{
    public IReadOnlyList<string>? RelationIds { get; init; }
    public RelationPropertyValue() : base("relation") { }
}

public sealed record RollupPropertyValue : PropertyValue
{
    public IReadOnlyList<PropertyValue>? RollupResults { get; init; }
    public RollupPropertyValue() : base("rollup") { }
}

public sealed record PeoplePropertyValue : PropertyValue
{
    public IReadOnlyList<User>? People { get; init; }
    public PeoplePropertyValue() : base("people") { }
}

public sealed record FilesPropertyValue : PropertyValue
{
    public IReadOnlyList<FileObject>? Files { get; init; }
    public FilesPropertyValue() : base("files") { }
}

public sealed record CheckboxPropertyValue : PropertyValue
{
    public bool? Checkbox { get; init; }
    public CheckboxPropertyValue() : base("checkbox") { }
}

public sealed record UrlPropertyValue : PropertyValue
{
    public string? Url { get; init; }
    public UrlPropertyValue() : base("url") { }
}
