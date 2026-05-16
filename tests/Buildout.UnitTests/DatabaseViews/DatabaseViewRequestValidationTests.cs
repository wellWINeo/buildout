using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.DatabaseViews;

public sealed class DatabaseViewRequestValidationTests
{
    private readonly IBuildinClient _client = Substitute.For<IBuildinClient>();
    private readonly IPropertyValueFormatter _formatter = Substitute.For<IPropertyValueFormatter>();
    private readonly IDatabaseViewStyle _tableStyle = Substitute.For<IDatabaseViewStyle>();
    private readonly CellBudget _budget = new(24, "…");

    private DatabaseViewRenderer CreateRenderer()
    {
        var styles = new Dictionary<DatabaseViewStyle, IDatabaseViewStyle>
        {
            [DatabaseViewStyle.Table] = _tableStyle,
        };
        return new DatabaseViewRenderer(_client, _formatter, styles, _budget, NullLogger<DatabaseViewRenderer>.Instance);
    }

    [Fact]
    public async Task Empty_database_id_throws_ValidationException()
    {
        var renderer = CreateRenderer();
        var request = new DatabaseViewRequest("", DatabaseViewStyle.Table, null, null);

        var ex = await Assert.ThrowsAsync<DatabaseViewValidationException>(
            () => renderer.RenderAsync(request));

        Assert.Equal(nameof(DatabaseViewRequest.DatabaseId), ex.OffendingField);
    }

    [Fact]
    public async Task Board_without_group_by_throws_ValidationException()
    {
        var renderer = CreateRenderer();
        var request = new DatabaseViewRequest("db-1", DatabaseViewStyle.Board, null, null);

        var ex = await Assert.ThrowsAsync<DatabaseViewValidationException>(
            () => renderer.RenderAsync(request));

        Assert.Equal("GroupByProperty", ex.OffendingField);
    }

    [Fact]
    public async Task Calendar_without_date_property_throws_ValidationException()
    {
        var renderer = CreateRenderer();
        var request = new DatabaseViewRequest("db-1", DatabaseViewStyle.Calendar, null, null);

        var ex = await Assert.ThrowsAsync<DatabaseViewValidationException>(
            () => renderer.RenderAsync(request));

        Assert.Equal("DateProperty", ex.OffendingField);
    }

    [Fact]
    public async Task Timeline_without_date_property_throws_ValidationException()
    {
        var renderer = CreateRenderer();
        var request = new DatabaseViewRequest("db-1", DatabaseViewStyle.Timeline, null, null);

        var ex = await Assert.ThrowsAsync<DatabaseViewValidationException>(
            () => renderer.RenderAsync(request));

        Assert.Equal("DateProperty", ex.OffendingField);
    }

    [Fact]
    public async Task Valid_table_request_does_not_throw_static_validation()
    {
        var renderer = CreateRenderer();
        var request = new DatabaseViewRequest("db-1", DatabaseViewStyle.Table, null, null);

        _client.GetDatabaseAsync(default!, default).ReturnsForAnyArgs(new Database
        {
            Id = "db-1",
            Properties = new Dictionary<string, PropertySchema> { ["Name"] = new TitlePropertySchema() }
        });
        _client.QueryDatabaseAsync(default!, default!, default).ReturnsForAnyArgs(
            new QueryDatabaseResult { HasMore = false });
        _tableStyle.Key.Returns(DatabaseViewStyle.Table);
        _tableStyle.Render(default!, default!, default!, default!, default!).ReturnsForAnyArgs("rendered");

        var result = await renderer.RenderAsync(request);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Valid_board_request_with_group_by_does_not_throw_static_validation()
    {
        var varStyle = Substitute.For<IDatabaseViewStyle>();
        varStyle.Key.Returns(DatabaseViewStyle.Board);
        varStyle.Render(default!, default!, default!, default!, default!).ReturnsForAnyArgs("rendered");

        var styles = new Dictionary<DatabaseViewStyle, IDatabaseViewStyle>
        {
            [DatabaseViewStyle.Board] = varStyle,
        };
        var renderer = new DatabaseViewRenderer(_client, _formatter, styles, _budget, NullLogger<DatabaseViewRenderer>.Instance);
        var request = new DatabaseViewRequest("db-1", DatabaseViewStyle.Board, "Status", null);

        _client.GetDatabaseAsync(default!, default).ReturnsForAnyArgs(new Database
        {
            Id = "db-1",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["Name"] = new TitlePropertySchema(),
                ["Status"] = new SelectPropertySchema(),
            }
        });
        _client.QueryDatabaseAsync(default!, default!, default).ReturnsForAnyArgs(
            new QueryDatabaseResult { HasMore = false });

        var result = await renderer.RenderAsync(request);

        Assert.NotNull(result);
    }
}
