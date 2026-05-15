using System.Diagnostics.Metrics;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Markdown.Authoring.Properties;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Buildout.UnitTests.Markdown.Authoring;

[Collection("MetricsTests")]
public sealed class PageCreatorLoggingTests : IDisposable
{
    private readonly IBuildinClient _client = Substitute.For<IBuildinClient>();
    private readonly IMarkdownToBlocksParser _parser = Substitute.For<IMarkdownToBlocksParser>();
    private readonly IDatabasePropertyValueParser _propertyParser = Substitute.For<IDatabasePropertyValueParser>();
    private readonly TestLogger _logger = new();
    private readonly ParentKindProbe _probe;

    private const string ParentPageId = "parent-page-id";

    private readonly MeterListener _meterListener;
    private readonly List<(string Name, KeyValuePair<string, object?>[] Tags, long Value)> _counterRecords = [];
    private readonly object _counterLock = new();

    public PageCreatorLoggingTests()
    {
        _probe = new ParentKindProbe(_client);

        _client.GetPageAsync(ParentPageId, Arg.Any<CancellationToken>())
            .Returns(new Page { Id = ParentPageId });

        _parser.Parse(Arg.Any<string>())
            .Returns(new AuthoredDocument { Title = "Test Page", Body = [] });

        _meterListener = new MeterListener();
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Buildout")
                listener.EnableMeasurementEvents(instrument);
        };
        _meterListener.SetMeasurementEventCallback<long>(OnMeasurementRecorded);
        _meterListener.Start();
    }

    public void Dispose()
    {
        _meterListener.Dispose();
    }

    private void OnMeasurementRecorded(
        Instrument instrument,
        long value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        lock (_counterLock)
            _counterRecords.Add((instrument.Name, tags.ToArray(), value));
    }

    private PageCreator CreateSut() =>
        new(_probe, _parser, _client, _propertyParser, _logger);

    private static BlockSubtreeWrite MakeBlock(string id = "") =>
        new() { Block = new ParagraphBlock { Id = id }, Children = [] };

    [Fact]
    public async Task CreateAsync_LogsOperationPageCreateWithParentKindAndBlockCount_OnSuccess()
    {
        var body = new List<BlockSubtreeWrite>
        {
            MakeBlock("b1"),
            MakeBlock("b2"),
            MakeBlock("b3")
        };
        _parser.Parse(Arg.Any<string>())
            .Returns(new AuthoredDocument { Title = "Logged Page", Body = body });

        _client.CreatePageAsync(Arg.Any<CreatePageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new Page { Id = "new-page-id" });

        var sut = CreateSut();
        await sut.CreateAsync(new CreatePageInput
        {
            ParentId = ParentPageId,
            Markdown = "# Logged Page"
        });

        var completedEntry = _logger.Entries.FirstOrDefault(e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("page_create") &&
            e.Message.Contains("completed"));

        Assert.NotNull(completedEntry);

        var startedEntry = _logger.Entries.FirstOrDefault(e =>
            e.Level == LogLevel.Debug &&
            e.Message.Contains("page_create") &&
            e.Message.Contains("started"));

        Assert.NotNull(startedEntry);
    }

    [Fact]
    public async Task CreateAsync_RecordsPagesCreatedTotalWithParentKindPage_OnSuccess()
    {
        _parser.Parse(Arg.Any<string>())
            .Returns(new AuthoredDocument { Title = "Metric Page", Body = [MakeBlock("b1")] });

        _client.CreatePageAsync(Arg.Any<CreatePageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new Page { Id = "metric-page-id" });

        var sut = CreateSut();
        await sut.CreateAsync(new CreatePageInput
        {
            ParentId = ParentPageId,
            Markdown = "# Metric Page"
        });

        var pagesCreatedRecords = _counterRecords
            .Where(r => r.Name == "buildout.pages.created.total")
            .ToList();

        Assert.NotEmpty(pagesCreatedRecords);

        var record = pagesCreatedRecords.Last();
        Assert.Equal(1, record.Value);

        var parentKindTag = record.Tags.FirstOrDefault(t => t.Key == "parent_kind");
        Assert.Equal("page", parentKindTag.Value);
    }

    [Fact]
    public async Task CreateAsync_RecordsBlocksProcessedTotalWithOperationPageCreate_OnSuccess()
    {
        var body = new List<BlockSubtreeWrite>
        {
            MakeBlock("b1"),
            MakeBlock("b2"),
            MakeBlock("b3")
        };
        _parser.Parse(Arg.Any<string>())
            .Returns(new AuthoredDocument { Title = "Blocks Page", Body = body });

        _client.CreatePageAsync(Arg.Any<CreatePageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new Page { Id = "blocks-page-id" });

        var sut = CreateSut();
        await sut.CreateAsync(new CreatePageInput
        {
            ParentId = ParentPageId,
            Markdown = "# Blocks Page"
        });

        var pageCreateBlockRecords = _counterRecords
            .Where(r => r.Name == "buildout.blocks.processed.total" &&
                r.Tags.Any(t => t.Key == "operation" && t.Value?.ToString() == "page_create"))
            .ToList();

        Assert.NotEmpty(pageCreateBlockRecords);
        Assert.Equal(3, pageCreateBlockRecords.Last().Value);
    }

    [Fact]
    public async Task CreateAsync_LogsOperationPageCreateFailed_OnAuthError()
    {
        _client.GetPageAsync(ParentPageId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BuildinApiException(new ApiError(401, null, "Unauthorized", null)));
        _client.GetDatabaseAsync(ParentPageId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BuildinApiException(new ApiError(401, null, "Unauthorized", null)));

        var sut = CreateSut();
        await sut.CreateAsync(new CreatePageInput
        {
            ParentId = ParentPageId,
            Markdown = "# Fail"
        });

        var failedEntry = _logger.Entries.FirstOrDefault(e =>
            e.Level == LogLevel.Error &&
            e.Message.Contains("page_create") &&
            e.Message.Contains("failed"));

        Assert.NotNull(failedEntry);
    }

    private sealed class TestLogger : ILogger<PageCreator>
    {
        public List<LogEntry> Entries { get; } = [];

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            Entries.Add(new LogEntry(logLevel, message));
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }

    private sealed record LogEntry(LogLevel Level, string Message);
}
