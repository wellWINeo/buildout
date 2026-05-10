using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;

namespace Buildout.Core.DatabaseViews.Styles;

public sealed record DatabaseRow(
    string PageId,
    IReadOnlyDictionary<string, PropertyValue> Properties);

public interface IDatabaseViewStyle
{
    DatabaseViewStyle Key { get; }
    string Render(
        Database database,
        IReadOnlyList<DatabaseRow> rows,
        DatabaseViewRequest request,
        IPropertyValueFormatter formatter,
        CellBudget budget);
}
