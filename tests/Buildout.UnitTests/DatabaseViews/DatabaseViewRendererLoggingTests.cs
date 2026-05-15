using System.Diagnostics;
using System.Diagnostics.Metrics;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.DatabaseViews;

[Collection("MetricsTests")]
public sealed class DatabaseViewRendererLoggingTests
{
    private readonly IBuildinClient _client;
    private readonly IPropertyValueFormatter _formatter;
    private readonly CellBudget _budget;
    private readonly Dictionary<DatabaseViewStyle, IDatabaseViewStyle> _styles;
    private readonly ILogger<DatabaseViewRenderer> _logger;
    private readonly DatabaseViewRenderer _renderer;

    public DatabaseViewRendererLoggingTests()
    {
        _client = Substitute.For<IBuildinClient>();
        _formatter = Substitute.For<IPropertyValueFormatter>();
        _budget = new CellBudget(24, "…");
        _styles = [];
        _logger = Substitute.For<ILogger<DatabaseViewRenderer>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _renderer = new DatabaseViewRenderer(_client, _formatter, _styles, _budget, _logger);
    }

    private static Database MakeDatabase(
        string id = "test-db-id",
        string? title = "Test DB",
        Dictionary<string, PropertySchema>? properties = null) => new()
    {
        Id = id,
        Title = title is not null
            ? [new RichText { Type = "text", Content = title }]
            : null,
        Properties = properties
    };

    private static Dictionary<string, PropertySchema> DefaultProperties => new()
    {
        ["Name"] = new TitlePropertySchema(),
    };

    private static DatabaseViewRequest MakeRequest(
        string databaseId = "test-db-id",
        DatabaseViewStyle style = DatabaseViewStyle.Table) => new(databaseId, style, null, null);

    private void SetupStyle(DatabaseViewStyle key, string renderedOutput)
    {
        var style = Substitute.For<IDatabaseViewStyle>();
        style.Key.Returns(key);
        style.Render(Arg.Any<Database>(), Arg.Any<IReadOnlyList<DatabaseRow>>(),
                Arg.Any<DatabaseViewRequest>(), Arg.Any<IPropertyValueFormatter>(), Arg.Any<CellBudget>())
            .Returns(renderedOutput);
        _styles[key] = style;
    }

    private void SetupSuccessfulQuery(params Dictionary<string, PropertyValue>[] results)
    {
        _client.QueryDatabaseAsync("test-db-id", Arg.Any<QueryDatabaseRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new QueryDatabaseResult
            {
                Results = results,
                HasMore = false
            }));
    }

    [Fact]
    public async Task RenderAsync_Success_LogsOperationWithDatabaseIdStyleAndRowCount()
    {
        var db = MakeDatabase(properties: DefaultProperties);
        _client.GetDatabaseAsync("test-db-id", Arg.Any<CancellationToken>()).Returns(Task.FromResult(db));

        SetupSuccessfulQuery(
            new Dictionary<string, PropertyValue>
            {
                ["Name"] = new TitlePropertyValue
                {
                    Title = [new RichText { Type = "text", Content = "Row 1" }]
                }
            });

        SetupStyle(DatabaseViewStyle.Table, "<rendered>");

        await _renderer.RenderAsync(MakeRequest(), CancellationToken.None);

#pragma warning disable CA1873
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("database_view") && v.ToString()!.Contains("database_id=test-db-id") && v.ToString()!.Contains("style=table") && v.ToString()!.Contains("row_count=1")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
#pragma warning restore CA1873
    }

    [Fact]
    public async Task RenderAsync_Success_RecordsDatabaseViewRendersTotalMetric()
    {
        long recordedValue = 0;
        KeyValuePair<string, object?>[] recordedTags = [];

        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Name == "buildout.database.view.renders.total" && inst.Meter.Name == "Buildout")
                l.EnableMeasurementEvents(inst);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
        {
            recordedValue += value;
            recordedTags = tags.ToArray();
        });
        listener.Start();

        var db = MakeDatabase(properties: DefaultProperties);
        _client.GetDatabaseAsync("test-db-id", Arg.Any<CancellationToken>()).Returns(Task.FromResult(db));
        SetupSuccessfulQuery();
        SetupStyle(DatabaseViewStyle.Table, "<rendered>");

        await _renderer.RenderAsync(MakeRequest(), CancellationToken.None);

        listener.RecordObservableInstruments();
        Assert.Equal(1, recordedValue);

        var tagDict = ToDictionary(recordedTags);
        Assert.Equal("table", tagDict["style"]);
    }

    [Fact]
    public async Task RenderAsync_Success_RecordsOperationsTotalWithSuccessOutcome()
    {
        long recordedValue = 0;
        KeyValuePair<string, object?>[] recordedTags = [];

        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Name == "buildout.operations.total" && inst.Meter.Name == "Buildout")
                l.EnableMeasurementEvents(inst);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
        {
            var tagArray = tags.ToArray();
            if (tagArray.Any(t => t.Key == "operation" && t.Value?.ToString() == "database_view"))
            {
                recordedValue += value;
                recordedTags = tagArray;
            }
        });
        listener.Start();

        var db = MakeDatabase(properties: DefaultProperties);
        _client.GetDatabaseAsync("test-db-id", Arg.Any<CancellationToken>()).Returns(Task.FromResult(db));
        SetupSuccessfulQuery();
        SetupStyle(DatabaseViewStyle.Table, "<rendered>");

        await _renderer.RenderAsync(MakeRequest(), CancellationToken.None);

        listener.RecordObservableInstruments();
        Assert.Equal(1, recordedValue);

        var tagDict = ToDictionary(recordedTags);
        Assert.Equal("database_view", tagDict["operation"]);
        Assert.Equal("success", tagDict["outcome"]);
    }

    private static Dictionary<string, object?> ToDictionary(KeyValuePair<string, object?>[] tags)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var tag in tags)
            dict[tag.Key] = tag.Value;
        return dict;
    }
}
