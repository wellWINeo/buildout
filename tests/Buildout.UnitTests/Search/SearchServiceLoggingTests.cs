using System.Diagnostics;
using System.Diagnostics.Metrics;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Search;
using Buildout.Core.Search.Internal;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Search;

[Collection("MetricsTests")]
public sealed class SearchServiceLoggingTests
{
    private readonly IBuildinClient _client;
    private readonly ITitleRenderer _titleRenderer;
    private readonly AncestorScopeFilter _scopeFilter;
    private readonly ILogger<SearchService> _logger;
    private readonly SearchService _service;

    public SearchServiceLoggingTests()
    {
        _client = Substitute.For<IBuildinClient>();
        _titleRenderer = Substitute.For<ITitleRenderer>();
        _scopeFilter = new AncestorScopeFilter(_client, Substitute.For<ILogger<AncestorScopeFilter>>());
        _logger = Substitute.For<ILogger<SearchService>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _service = new SearchService(_client, _titleRenderer, _scopeFilter, _logger);
    }

    private static Page MakePage(string id, bool archived = false, Parent? parent = null,
        IReadOnlyList<RichText>? title = null) => new()
    {
        Id = id,
        ObjectType = "page",
        Archived = archived,
        Parent = parent,
        Title = title
    };

    private void SetupSearchResponse(params Page[] pages) =>
        _client.SearchPagesAsync(Arg.Any<PageSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PageSearchResults { Results = pages }));

    private void SetupTitleRenderer(string output = "Title") =>
        _titleRenderer.RenderPlain(Arg.Any<IReadOnlyList<RichText>>()).Returns(output);

    [Fact]
    public async Task SearchAsync_Success_LogsOperationSearchWithQueryAndResultCount()
    {
        var pages = new[] { MakePage("p1"), MakePage("p2") };
        SetupSearchResponse(pages);
        SetupTitleRenderer();

        await _service.SearchAsync("test query", null, CancellationToken.None);

#pragma warning disable CA1873
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("search") && v.ToString()!.Contains("query=test query") && v.ToString()!.Contains("result_count=2")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
#pragma warning restore CA1873
    }

    [Fact]
    public async Task SearchAsync_Success_RecordsSearchResultsTotalMetric()
    {
        long recordedValue = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Name == "buildout.search.results.total" && inst.Meter.Name == "Buildout")
                l.EnableMeasurementEvents(inst);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
        {
            recordedValue += value;
        });
        listener.Start();

        var pages = new[] { MakePage("p1"), MakePage("p2"), MakePage("p3") };
        SetupSearchResponse(pages);
        SetupTitleRenderer();

        await _service.SearchAsync("test", null, CancellationToken.None);

        listener.RecordObservableInstruments();
        Assert.Equal(3, recordedValue);
    }

    [Fact]
    public async Task SearchAsync_Success_RecordsOperationsTotalWithSearchAndSuccess()
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
            if (tagArray.Any(t => t.Key == "operation" && t.Value?.ToString() == "search"))
            {
                recordedValue += value;
                recordedTags = tagArray;
            }
        });
        listener.Start();

        var pages = new[] { MakePage("p1") };
        SetupSearchResponse(pages);
        SetupTitleRenderer();

        await _service.SearchAsync("test", null, CancellationToken.None);

        listener.RecordObservableInstruments();
        Assert.Equal(1, recordedValue);

        var tagDict = ToDictionary(recordedTags);
        Assert.Equal("search", tagDict["operation"]);
        Assert.Equal("success", tagDict["outcome"]);
    }

    [Fact]
    public async Task SearchAsync_ApiException_RecordsOperationsTotalWithFailure()
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
            if (tagArray.Any(t => t.Key == "operation" && t.Value?.ToString() == "search"))
            {
                recordedValue += value;
                recordedTags = tagArray;
            }
        });
        listener.Start();

        _client.SearchPagesAsync(Arg.Any<PageSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<PageSearchResults>>(_ => throw new BuildinApiException(
                new ApiError(500, "internal_error", "Internal", null)));

        await Assert.ThrowsAsync<BuildinApiException>(
            () => _service.SearchAsync("test", null, CancellationToken.None));

        listener.RecordObservableInstruments();
        Assert.Equal(1, recordedValue);

        var tagDict = ToDictionary(recordedTags);
        Assert.Equal("search", tagDict["operation"]);
        Assert.Equal("failure", tagDict["outcome"]);
    }

    [Fact]
    public async Task SearchAsync_LongQuery_TruncatesInTag()
    {
        var longQuery = new string('a', 150);
        SetupSearchResponse();
        SetupTitleRenderer();

        await _service.SearchAsync(longQuery, null, CancellationToken.None);

        var truncated = string.Concat(longQuery.AsSpan(0, 100), "…");
#pragma warning disable CA1873
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("query=" + truncated)),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
#pragma warning restore CA1873
    }

    private static Dictionary<string, object?> ToDictionary(KeyValuePair<string, object?>[] tags)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var tag in tags)
            dict[tag.Key] = tag.Value;
        return dict;
    }
}
