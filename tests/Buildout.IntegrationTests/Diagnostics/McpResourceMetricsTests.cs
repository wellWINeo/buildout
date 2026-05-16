using System.Diagnostics.Metrics;
using System.IO.Pipelines;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Markdown;
using Buildout.Mcp.Resources;
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
public sealed class McpResourceMetricsTests : IAsyncLifetime
{
    private readonly IPageMarkdownRenderer _renderer = Substitute.For<IPageMarkdownRenderer>();
    private ServiceProvider _sp = null!;
    private McpServer _server = null!;
    private McpClient _client = null!;
    private Pipe _c2s = null!;
    private Pipe _s2c = null!;

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPageMarkdownRenderer>(_renderer);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));

        services.AddMcpServer().WithResources<PageResourceHandler>();

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
    public async Task ReadPage_Success_RecordsMetric()
    {
        _renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("# Hello");

        using var collector = new ResourceMetricCollector();

        await _client.ReadResourceAsync("buildin://abc-123");

        var measurement = Assert.Single(collector.Measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("success", measurement.Tags["outcome"]);
    }

    [Fact]
    public async Task ReadPage_NotFound_RecordsFailureMetric()
    {
        _renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new BuildinApiException(
                new ApiError(404, "not_found", "Page not found", null)));

        using var collector = new ResourceMetricCollector();

        await Assert.ThrowsAsync<McpProtocolException>(() =>
            _client.ReadResourceAsync("buildin://nonexistent").AsTask());

        var measurement = Assert.Single(collector.Measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("failure", measurement.Tags["outcome"]);
    }

    [Fact]
    public async Task ReadPage_AuthError_RecordsFailureMetric()
    {
        _renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new BuildinApiException(
                new ApiError(403, "forbidden", "Forbidden", null)));

        using var collector = new ResourceMetricCollector();

        await Assert.ThrowsAsync<McpProtocolException>(() =>
            _client.ReadResourceAsync("buildin://denied").AsTask());

        var measurement = Assert.Single(collector.Measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("failure", measurement.Tags["outcome"]);
    }

    [Fact]
    public async Task ReadPage_MetricsDoNotSwallowErrors()
    {
        _renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new BuildinApiException(
                new TransportError(new HttpRequestException("Connection refused"))));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(() =>
            _client.ReadResourceAsync("buildin://transport-fail").AsTask());

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
    }

    [Fact]
    public async Task ReadPage_SuccessAndFailure_RecordSeparateMeasurements()
    {
        _renderer.RenderAsync("page-ok", Arg.Any<CancellationToken>())
            .Returns("# OK");

        _renderer.RenderAsync("page-bad", Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new BuildinApiException(
                new ApiError(404, "not_found", "Not found", null)));

        using var collector = new ResourceMetricCollector();

        await _client.ReadResourceAsync("buildin://page-ok");

        await Assert.ThrowsAsync<McpProtocolException>(() =>
            _client.ReadResourceAsync("buildin://page-bad").AsTask());

        Assert.Equal(2, collector.Measurements.Count);

        var success = collector.Measurements.First(m => m.Tags["outcome"]?.Equals("success") == true);
        Assert.Equal(1, success.Value);

        var failure = collector.Measurements.First(m => m.Tags["outcome"]?.Equals("failure") == true);
        Assert.Equal(1, failure.Value);
    }

    private sealed class ResourceMetricCollector : IDisposable
    {
        private readonly MeterListener _listener = new();
        public List<(double Value, Dictionary<string, object?> Tags)> Measurements { get; } = [];

        public ResourceMetricCollector()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "buildout.mcp.resource.reads.total" && instrument.Meter.Name == "Buildout")
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>(Record);
            _listener.Start();
        }

        private void Record(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var kv in tags)
                dict[kv.Key] = kv.Value;
            Measurements.Add((value, dict));
        }

        public void Dispose() => _listener.Dispose();
    }
}
