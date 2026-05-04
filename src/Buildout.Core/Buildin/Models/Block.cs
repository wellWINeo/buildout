namespace Buildout.Core.Buildin.Models;

public abstract record Block
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? LastEditedAt { get; init; }
    public bool HasChildren { get; init; }
    public Parent? Parent { get; init; }

    private protected Block(string type) => Type = type;
}

public sealed record ParagraphBlock : Block
{
    public IReadOnlyList<RichText>? RichTextContent { get; init; }
    public ParagraphBlock() : base("paragraph") { }
}

public sealed record Heading1Block : Block
{
    public IReadOnlyList<RichText>? RichTextContent { get; init; }
    public Heading1Block() : base("heading_1") { }
}

public sealed record Heading2Block : Block
{
    public IReadOnlyList<RichText>? RichTextContent { get; init; }
    public Heading2Block() : base("heading_2") { }
}

public sealed record Heading3Block : Block
{
    public IReadOnlyList<RichText>? RichTextContent { get; init; }
    public Heading3Block() : base("heading_3") { }
}

public sealed record BulletedListItemBlock : Block
{
    public IReadOnlyList<RichText>? RichTextContent { get; init; }
    public BulletedListItemBlock() : base("bulleted_list_item") { }
}

public sealed record NumberedListItemBlock : Block
{
    public IReadOnlyList<RichText>? RichTextContent { get; init; }
    public NumberedListItemBlock() : base("numbered_list_item") { }
}

public sealed record ToDoBlock : Block
{
    public IReadOnlyList<RichText>? RichTextContent { get; init; }
    public bool? Checked { get; init; }
    public ToDoBlock() : base("to_do") { }
}

public sealed record ToggleBlock : Block
{
    public IReadOnlyList<RichText>? RichTextContent { get; init; }
    public ToggleBlock() : base("toggle") { }
}

public sealed record CodeBlock : Block
{
    public IReadOnlyList<RichText>? RichTextContent { get; init; }
    public string? Language { get; init; }
    public CodeBlock() : base("code") { }
}

public sealed record QuoteBlock : Block
{
    public IReadOnlyList<RichText>? RichTextContent { get; init; }
    public QuoteBlock() : base("quote") { }
}

public sealed record DividerBlock : Block
{
    public DividerBlock() : base("divider") { }
}

public sealed record ImageBlock : Block
{
    public string? Url { get; init; }
    public IReadOnlyList<RichText>? Caption { get; init; }
    public ImageBlock() : base("image") { }
}

public sealed record EmbedBlock : Block
{
    public string? Url { get; init; }
    public EmbedBlock() : base("embed") { }
}

public sealed record TableBlock : Block
{
    public int? TableWidth { get; init; }
    public bool? HasColumnHeader { get; init; }
    public bool? HasRowHeader { get; init; }
    public TableBlock() : base("table") { }
}

public sealed record TableRowBlock : Block
{
    public IReadOnlyList<IReadOnlyList<RichText>>? Cells { get; init; }
    public TableRowBlock() : base("table_row") { }
}

public sealed record ColumnListBlock : Block
{
    public ColumnListBlock() : base("column_list") { }
}

public sealed record ColumnBlock : Block
{
    public ColumnBlock() : base("column") { }
}

public sealed record ChildPageBlock : Block
{
    public string? Title { get; init; }
    public ChildPageBlock() : base("child_page") { }
}

public sealed record ChildDatabaseBlock : Block
{
    public string? Title { get; init; }
    public ChildDatabaseBlock() : base("child_database") { }
}

public sealed record SyncedBlock : Block
{
    public string? SyncedFromId { get; init; }
    public SyncedBlock() : base("synced_block") { }
}

public sealed record LinkPreviewBlock : Block
{
    public string? Url { get; init; }
    public LinkPreviewBlock() : base("link_preview") { }
}

public sealed record UnsupportedBlock : Block
{
    public UnsupportedBlock() : base("unsupported") { }
}
