using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.DatabaseViews;

public sealed class DatabaseViewRendererTests
{
    private readonly IBuildinClient _client;
    private readonly IPropertyValueFormatter _formatter;
    private readonly CellBudget _budget;
    private readonly Dictionary<DatabaseViewStyle, IDatabaseViewStyle> _styles;
    private readonly DatabaseViewRenderer _renderer;

    public DatabaseViewRendererTests()
    {
        _client = Substitute.For<IBuildinClient>();
        _formatter = Substitute.For<IPropertyValueFormatter>();
        _budget = new CellBudget(24, "\u2026");
        _styles = [];

        _renderer = new DatabaseViewRenderer(_client, _formatter, _styles, _budget);
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
        ["Status"] = new SelectPropertySchema
        {
            Options = [new SelectOption { Id = "s1", Name = "Todo" }, new SelectOption { Id = "s2", Name = "Done" }]
        },
        ["Due"] = new DatePropertySchema(),
    };

    private static DatabaseViewRequest MakeRequest(
        string databaseId = "test-db-id",
        DatabaseViewStyle style = DatabaseViewStyle.Table,
        string? groupByProperty = null,
        string? dateProperty = null) => new(databaseId, style, groupByProperty, dateProperty);

    private void SetupStyle(DatabaseViewStyle key, string renderedOutput)
    {
        var style = Substitute.For<IDatabaseViewStyle>();
        style.Key.Returns(key);
        style.Render(Arg.Any<Database>(), Arg.Any<IReadOnlyList<DatabaseRow>>(),
            Arg.Any<DatabaseViewRequest>(), Arg.Any<IPropertyValueFormatter>(), Arg.Any<CellBudget>())
            .Returns(renderedOutput);
        _styles[key] = style;
    }

    [Fact]
    public async Task EmptyDatabaseId_ThrowsValidationException()
    {
        var request = MakeRequest(databaseId: "");

        var ex = await Assert.ThrowsAsync<DatabaseViewValidationException>(
            () => _renderer.RenderAsync(request, CancellationToken.None));

        Assert.Equal("DatabaseId", ex.OffendingField);
        await _client.DidNotReceive().GetDatabaseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BoardWithoutGroupBy_ThrowsValidationException()
    {
        var request = MakeRequest(style: DatabaseViewStyle.Board, groupByProperty: null);

        var ex = await Assert.ThrowsAsync<DatabaseViewValidationException>(
            () => _renderer.RenderAsync(request, CancellationToken.None));

        Assert.Equal("GroupByProperty", ex.OffendingField);
        await _client.DidNotReceive().GetDatabaseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CalendarWithoutDateProperty_ThrowsValidationException()
    {
        var request = MakeRequest(style: DatabaseViewStyle.Calendar, dateProperty: null);

        var ex = await Assert.ThrowsAsync<DatabaseViewValidationException>(
            () => _renderer.RenderAsync(request, CancellationToken.None));

        Assert.Equal("DateProperty", ex.OffendingField);
        await _client.DidNotReceive().GetDatabaseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TimelineWithoutDateProperty_ThrowsValidationException()
    {
        var request = MakeRequest(style: DatabaseViewStyle.Timeline, dateProperty: null);

        var ex = await Assert.ThrowsAsync<DatabaseViewValidationException>(
            () => _renderer.RenderAsync(request, CancellationToken.None));

        Assert.Equal("DateProperty", ex.OffendingField);
        await _client.DidNotReceive().GetDatabaseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidSinglePageQuery_RendersSuccessfully()
    {
        var db = MakeDatabase(properties: DefaultProperties);
        _client.GetDatabaseAsync("test-db-id", Arg.Any<CancellationToken>()).Returns(Task.FromResult(db));

        _client.QueryDatabaseAsync("test-db-id", Arg.Any<QueryDatabaseRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new QueryDatabaseResult
            {
                Results =
                [
                    new Dictionary<string, PropertyValue>
                    {
                        ["Name"] = new TitlePropertyValue
                        {
                            Title = [new RichText { Type = "text", Content = "Row 1" }]
                        }
                    }
                ],
                HasMore = false
            }));

        SetupStyle(DatabaseViewStyle.Table, "<table-rendered>");

        var result = await _renderer.RenderAsync(MakeRequest(), CancellationToken.None);

        Assert.Contains("<table-rendered>", result);
        Assert.Contains("Test DB", result);
    }

    [Fact]
    public async Task MultiPagePagination_FollowsCursors()
    {
        var db = MakeDatabase(properties: DefaultProperties);
        _client.GetDatabaseAsync("test-db-id", Arg.Any<CancellationToken>()).Returns(Task.FromResult(db));

        _client.QueryDatabaseAsync("test-db-id",
                Arg.Is<QueryDatabaseRequest>(r => r.StartCursor == null), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new QueryDatabaseResult
            {
                Results =
                [
                    new Dictionary<string, PropertyValue>
                    {
                        ["Name"] = new TitlePropertyValue
                        {
                            Title = [new RichText { Type = "text", Content = "Row 1" }]
                        }
                    }
                ],
                HasMore = true,
                NextCursor = "cursor-1"
            }));

        _client.QueryDatabaseAsync("test-db-id",
                Arg.Is<QueryDatabaseRequest>(r => r.StartCursor == "cursor-1"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new QueryDatabaseResult
            {
                Results =
                [
                    new Dictionary<string, PropertyValue>
                    {
                        ["Name"] = new TitlePropertyValue
                        {
                            Title = [new RichText { Type = "text", Content = "Row 2" }]
                        }
                    }
                ],
                HasMore = false
            }));

        SetupStyle(DatabaseViewStyle.Table, "<result>");

        var result = await _renderer.RenderAsync(MakeRequest(), CancellationToken.None);

        await _client.Received(2).QueryDatabaseAsync("test-db-id", Arg.Any<QueryDatabaseRequest>(), Arg.Any<CancellationToken>());
        Assert.Contains("<result>", result);
    }

    [Fact]
    public async Task TransportError_PropagatedUnchanged()
    {
        var inner = new HttpRequestException("network failure");
        var transportError = new TransportError(inner);
        var apiException = new BuildinApiException(transportError);

        _client.GetDatabaseAsync("test-db-id", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Database>(apiException));

        var ex = await Assert.ThrowsAsync<BuildinApiException>(
            () => _renderer.RenderAsync(MakeRequest(), CancellationToken.None));

        Assert.Same(transportError, ex.Error);
    }

    [Fact]
    public async Task Cancellation_Propagated()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _client.GetDatabaseAsync("test-db-id", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Database>(new OperationCanceledException(cts.Token)));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _renderer.RenderAsync(MakeRequest(), cts.Token));
    }

    [Fact]
    public async Task DispatchesToCorrectStyleStrategy()
    {
        var db = MakeDatabase(properties: DefaultProperties);
        _client.GetDatabaseAsync("test-db-id", Arg.Any<CancellationToken>()).Returns(Task.FromResult(db));
        _client.QueryDatabaseAsync("test-db-id", Arg.Any<QueryDatabaseRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new QueryDatabaseResult { HasMore = false }));

        var tableStyle = Substitute.For<IDatabaseViewStyle>();
        tableStyle.Key.Returns(DatabaseViewStyle.Table);
        tableStyle.Render(Arg.Any<Database>(), Arg.Any<IReadOnlyList<DatabaseRow>>(),
                Arg.Any<DatabaseViewRequest>(), Arg.Any<IPropertyValueFormatter>(), Arg.Any<CellBudget>())
            .Returns("table-output");

        var boardStyle = Substitute.For<IDatabaseViewStyle>();
        boardStyle.Key.Returns(DatabaseViewStyle.Board);
        boardStyle.Render(Arg.Any<Database>(), Arg.Any<IReadOnlyList<DatabaseRow>>(),
                Arg.Any<DatabaseViewRequest>(), Arg.Any<IPropertyValueFormatter>(), Arg.Any<CellBudget>())
            .Returns("board-output");

        _styles[DatabaseViewStyle.Table] = tableStyle;
        _styles[DatabaseViewStyle.Board] = boardStyle;

        var result = await _renderer.RenderAsync(MakeRequest(style: DatabaseViewStyle.Board, groupByProperty: "Status"),
            CancellationToken.None);

        Assert.Contains("board-output", result);
        boardStyle.Received(1).Render(
            Arg.Any<Database>(),
            Arg.Any<IReadOnlyList<DatabaseRow>>(),
            Arg.Any<DatabaseViewRequest>(),
            Arg.Any<IPropertyValueFormatter>(),
            Arg.Any<CellBudget>());
        tableStyle.DidNotReceive().Render(
            Arg.Any<Database>(),
            Arg.Any<IReadOnlyList<DatabaseRow>>(),
            Arg.Any<DatabaseViewRequest>(),
            Arg.Any<IPropertyValueFormatter>(),
            Arg.Any<CellBudget>());
    }

    [Fact]
    public async Task SchemaValidation_GroupByPropertyNotInSchema_ThrowsWithAlternatives()
    {
        var db = MakeDatabase(properties: DefaultProperties);
        _client.GetDatabaseAsync("test-db-id", Arg.Any<CancellationToken>()).Returns(Task.FromResult(db));

        var request = MakeRequest(style: DatabaseViewStyle.Board, groupByProperty: "Nonexistent");

        var ex = await Assert.ThrowsAsync<DatabaseViewValidationException>(
            () => _renderer.RenderAsync(request, CancellationToken.None));

        Assert.Equal("GroupByProperty", ex.OffendingField);
        Assert.Contains("Status", ex.ValidAlternatives);
    }

    [Fact]
    public async Task SchemaValidation_DatePropertyNotInSchema_ThrowsWithAlternatives()
    {
        var db = MakeDatabase(properties: DefaultProperties);
        _client.GetDatabaseAsync("test-db-id", Arg.Any<CancellationToken>()).Returns(Task.FromResult(db));

        var request = MakeRequest(style: DatabaseViewStyle.Calendar, dateProperty: "Nonexistent");

        var ex = await Assert.ThrowsAsync<DatabaseViewValidationException>(
            () => _renderer.RenderAsync(request, CancellationToken.None));

        Assert.Equal("DateProperty", ex.OffendingField);
        Assert.Contains("Due", ex.ValidAlternatives);
    }

    [Fact]
    public async Task RenderInlineAsync_RendersWithTableStyle()
    {
        var db = MakeDatabase(properties: DefaultProperties);
        _client.GetDatabaseAsync("test-db-id", Arg.Any<CancellationToken>()).Returns(Task.FromResult(db));
        _client.QueryDatabaseAsync("test-db-id", Arg.Any<QueryDatabaseRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new QueryDatabaseResult { HasMore = false }));

        SetupStyle(DatabaseViewStyle.Table, "<inline-table>");

        var result = await _renderer.RenderInlineAsync("test-db-id", CancellationToken.None);

        Assert.Contains("<inline-table>", result);
        Assert.Contains("## Test DB", result);
    }

    [Fact]
    public async Task UnknownStyle_ThrowsValidationException()
    {
        var db = MakeDatabase(properties: DefaultProperties);
        _client.GetDatabaseAsync("test-db-id", Arg.Any<CancellationToken>()).Returns(Task.FromResult(db));
        _client.QueryDatabaseAsync("test-db-id", Arg.Any<QueryDatabaseRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new QueryDatabaseResult { HasMore = false }));

        _styles.Clear();

        var request = MakeRequest(style: DatabaseViewStyle.Gallery);

        var ex = await Assert.ThrowsAsync<DatabaseViewValidationException>(
            () => _renderer.RenderAsync(request, CancellationToken.None));

        Assert.Contains("Gallery", ex.Message);
    }
}
