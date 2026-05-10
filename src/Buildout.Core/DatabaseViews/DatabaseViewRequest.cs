namespace Buildout.Core.DatabaseViews;

public sealed record DatabaseViewRequest(
    string DatabaseId,
    DatabaseViewStyle Style,
    string? GroupByProperty,
    string? DateProperty);
