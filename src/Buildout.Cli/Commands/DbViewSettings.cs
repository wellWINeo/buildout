using System.ComponentModel;
using Buildout.Core.DatabaseViews;
using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public sealed class DbViewSettings : DbSettings
{
    [CommandArgument(0, "<DATABASE_ID>")]
    public required string DatabaseId { get; init; }

    [CommandOption("-s|--style")]
    [Description("View style: table, board, gallery, list, calendar, timeline")]
    [DefaultValue(DatabaseViewStyle.Table)]
    public DatabaseViewStyle Style { get; init; }

    [CommandOption("-g|--group-by")]
    [Description("Property name to group by (required for board view)")]
    public string? GroupByProperty { get; init; }

    [CommandOption("-d|--date-property")]
    [Description("Property name carrying a date (required for calendar/timeline)")]
    public string? DateProperty { get; init; }
}
