using System.Diagnostics.Metrics;
using System.IO.Pipelines;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.DatabaseViews;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Search;
using Buildout.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Xunit;

namespace Buildout.IntegrationTests.Diagnostics;

[Collection("MetricsTests")]
public sealed class McpToolMetricsTests : IAsyncLifetime
{
    private readonly ISearchService _searchService = Substitute.For<ISearchService>();
    private readonly IDatabaseViewRenderer _dbRenderer = Substitute.For<IDatabaseViewRenderer>();
    private readonly IPageCreator _pageCreator = Substitute.For<IPageCreator>();
    private ServiceProvider _sp = null!;
    private McpServer _server = null!;
    private McpClient _client = null!;
    private Pipe _c2s = null!;
    private Pipe _s2c = null!;

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISearchService>(_searchService);
        services.AddSingleton<ISearchResultFormatter, SearchResultFormatter>();
        services.AddSingleton<IDatabaseViewRenderer>(_dbRenderer);
        services.AddSingleton<IPageCreator>(_pageCreator);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));

        services.AddMcpServer()
            .WithTools<SearchToolHandler>()
            .WithTools<DatabaseViewToolHandler>()
            .WithTools<CreatePageToolHandler>();

        _sp = services.BuildServiceProvider();

        var options = _sp.GetRequiredService<IOptions<McpServerOptions>>().Value;

        _c2s = new Pipe();
        _s2c = new Pipe();

        _server = McpServer.Create(
            new StreamServerTransport(
                _c2s.Reader.AsStream(),
                _s2c.Writer.AsStream()),
            options,
            _sp.GetRequiredService<ILoggerFactory>(),
            _sp);

        _ = _server.RunAsync();

        _client = await McpClient.CreateAsync(
            new StreamClientTransport(
                _c2s.Writer.AsStream(),
                _s2c.Reader.AsStream()),
            new McpClientOptions(),
            _sp.GetRequiredService<ILoggerFactory>());
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        await _server.DisposeAsync();
        _c2s.Writer.Complete();
        _c2s.Reader.Complete();
        _s2c.Writer.Complete();
        _s2c.Reader.Complete();
        await _sp.DisposeAsync();
    }

    [Fact]
    public async Task SearchTool_Success_RecordsMetric()
    {
        _searchService.SearchAsync("test", null, Arg.Any<CancellationToken>())
            .Returns(new List<SearchMatch>());

        using var collector = new BuildoutMetricCollector("buildout.mcp.tool.invocations.total");

        await _client.CallToolAsync("search", new Dictionary<string, object?> { ["query"] = "test" });

        var measurement = Assert.Single(collector.Measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("search", measurement.Tags["tool"]);
        Assert.Equal("success", measurement.Tags["outcome"]);
    }

    [Fact]
    public async Task SearchTool_Failure_RecordsMetric()
    {
        using var collector = new BuildoutMetricCollector("buildout.mcp.tool.invocations.total");

        await Assert.ThrowsAsync<McpProtocolException>(() =>
            _client.CallToolAsync("search", new Dictionary<string, object?> { ["query"] = "" }).AsTask());

        var measurement = Assert.Single(collector.Measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("search", measurement.Tags["tool"]);
        Assert.Equal("failure", measurement.Tags["outcome"]);
    }

    [Fact]
    public async Task DatabaseViewTool_Success_RecordsMetric()
    {
        _dbRenderer.RenderAsync(Arg.Any<DatabaseViewRequest>(), Arg.Any<CancellationToken>())
            .Returns("table output");

        using var collector = new BuildoutMetricCollector("buildout.mcp.tool.invocations.total");

        await _client.CallToolAsync("database_view", new Dictionary<string, object?> { ["database_id"] = "db-1" });

        var measurement = Assert.Single(collector.Measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("database_view", measurement.Tags["tool"]);
        Assert.Equal("success", measurement.Tags["outcome"]);
    }

    [Fact]
    public async Task DatabaseViewTool_Failure_RecordsMetric()
    {
        _dbRenderer.RenderAsync(Arg.Any<DatabaseViewRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new BuildinApiException(
                new ApiError(401, "unauthorized", "Unauthorized", null)));

        using var collector = new BuildoutMetricCollector("buildout.mcp.tool.invocations.total");

        await Assert.ThrowsAsync<McpProtocolException>(() =>
            _client.CallToolAsync("database_view", new Dictionary<string, object?> { ["database_id"] = "db-1" }).AsTask());

        var measurement = Assert.Single(collector.Measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("database_view", measurement.Tags["tool"]);
        Assert.Equal("failure", measurement.Tags["outcome"]);
    }

    [Fact]
    public async Task CreatePageTool_Success_RecordsMetric()
    {
        _pageCreator.CreateAsync(Arg.Any<CreatePageInput>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePageOutcome { NewPageId = "new-page-1", ResolvedTitle = "Test" });

        using var collector = new BuildoutMetricCollector("buildout.mcp.tool.invocations.total");

        await _client.CallToolAsync("create_page", new Dictionary<string, object?>
        {
            ["parent_id"] = "parent-1",
            ["markdown"] = "# Hello",
        });

        var measurement = Assert.Single(collector.Measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("create_page", measurement.Tags["tool"]);
        Assert.Equal("success", measurement.Tags["outcome"]);
    }

    [Fact]
    public async Task CreatePageTool_Failure_RecordsMetric()
    {
        _pageCreator.CreateAsync(Arg.Any<CreatePageInput>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePageOutcome
            {
                NewPageId = "partial-1",
                FailureClass = FailureClass.NotFound,
                UnderlyingException = new InvalidOperationException("not found"),
            });

        using var collector = new BuildoutMetricCollector("buildout.mcp.tool.invocations.total");

        await Assert.ThrowsAsync<McpProtocolException>(() =>
            _client.CallToolAsync("create_page", new Dictionary<string, object?>
            {
                ["parent_id"] = "parent-1",
                ["markdown"] = "# Hello",
            }).AsTask());

        var measurement = Assert.Single(collector.Measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("create_page", measurement.Tags["tool"]);
        Assert.Equal("failure", measurement.Tags["outcome"]);
    }

    [Fact]
    public async Task SearchTool_Success_RecordsDuration()
    {
        _searchService.SearchAsync("dur", null, Arg.Any<CancellationToken>())
            .Returns(new List<SearchMatch>());

        using var collector = new BuildoutMetricCollector("buildout.mcp.tool.duration");

        await _client.CallToolAsync("search", new Dictionary<string, object?> { ["query"] = "dur" });

        var measurement = Assert.Single(collector.Measurements);
        Assert.True(measurement.Value >= 0);
        Assert.Equal("search", measurement.Tags["tool"]);
        Assert.Equal("success", measurement.Tags["outcome"]);
    }

    [Fact]
    public async Task SearchTool_MetricsDoNotSwallowErrors()
    {
        _searchService.SearchAsync("auth", null, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SearchMatch>>>(_ => throw new BuildinApiException(
                new ApiError(401, "unauthorized", "Unauthorized", null)));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(() =>
            _client.CallToolAsync("search", new Dictionary<string, object?> { ["query"] = "auth" }).AsTask());

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
    }

    private sealed class BuildoutMetricCollector : IDisposable
    {
        private readonly MeterListener _listener = new();
        public List<(double Value, Dictionary<string, object?> Tags)> Measurements { get; } = [];

        public BuildoutMetricCollector(string instrumentName)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == instrumentName && instrument.Meter.Name == "Buildout")
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>(RecordLong);
            _listener.SetMeasurementEventCallback<double>(RecordDouble);
            _listener.Start();
        }

        private void RecordLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
            => Record(value, tags);

        private void RecordDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
            => Record(value, tags);

        private void Record(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var kv in tags)
                dict[kv.Key] = kv.Value;
            Measurements.Add((value, dict));
        }

        public void Dispose() => _listener.Dispose();
    }
}
