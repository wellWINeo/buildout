using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews.Rendering;

namespace Buildout.Core.DatabaseViews.Properties;

public interface IPropertyValueFormatter
{
    string Format(PropertyValue value, CellBudget budget);
}
