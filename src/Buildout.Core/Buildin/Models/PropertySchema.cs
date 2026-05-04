namespace Buildout.Core.Buildin.Models;

public abstract record PropertySchema
{
    public string Type { get; }
    public string? Name { get; init; }

    private protected PropertySchema(string type) => Type = type;
}

public sealed record TitlePropertySchema : PropertySchema
{
    public TitlePropertySchema() : base("title") { }
}

public sealed record RichTextPropertySchema : PropertySchema
{
    public RichTextPropertySchema() : base("rich_text") { }
}

public sealed record NumberPropertySchema : PropertySchema
{
    public string? Format { get; init; }
    public NumberPropertySchema() : base("number") { }
}

public sealed record SelectPropertySchema : PropertySchema
{
    public IReadOnlyList<SelectOption>? Options { get; init; }
    public SelectPropertySchema() : base("select") { }
}

public sealed record MultiSelectPropertySchema : PropertySchema
{
    public IReadOnlyList<SelectOption>? Options { get; init; }
    public MultiSelectPropertySchema() : base("multi_select") { }
}

public sealed record DatePropertySchema : PropertySchema
{
    public DatePropertySchema() : base("date") { }
}

public sealed record FormulaPropertySchema : PropertySchema
{
    public string? Expression { get; init; }
    public FormulaPropertySchema() : base("formula") { }
}

public sealed record RelationPropertySchema : PropertySchema
{
    public string? DatabaseId { get; init; }
    public RelationPropertySchema() : base("relation") { }
}

public sealed record RollupPropertySchema : PropertySchema
{
    public string? RelationName { get; init; }
    public string? RollupPropertyName { get; init; }
    public string? Function { get; init; }
    public RollupPropertySchema() : base("rollup") { }
}

public sealed record PeoplePropertySchema : PropertySchema
{
    public PeoplePropertySchema() : base("people") { }
}

public sealed record FilesPropertySchema : PropertySchema
{
    public FilesPropertySchema() : base("files") { }
}

public sealed record CheckboxPropertySchema : PropertySchema
{
    public CheckboxPropertySchema() : base("checkbox") { }
}

public sealed record UrlPropertySchema : PropertySchema
{
    public UrlPropertySchema() : base("url") { }
}

public sealed record CreatedTimePropertySchema : PropertySchema
{
    public CreatedTimePropertySchema() : base("created_time") { }
}
