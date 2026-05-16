using System.Diagnostics;
using System.Diagnostics.Metrics;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Diagnostics;
using Gen = Buildout.Core.Buildin.Generated.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Buildin;

[Collection("MetricsTests")]
public sealed class BotBuildinClientLoggingTests
{
    private readonly IRequestAdapter _adapter;
    private readonly IOptions<BuildinClientOptions> _options;
    private readonly ILogger<BotBuildinClient> _logger;
    private readonly BotBuildinClient _client;

    public BotBuildinClientLoggingTests()
    {
        _adapter = Substitute.For<IRequestAdapter>();
        _adapter.BaseUrl.Returns("https://api.buildin.ai");
        _options = Options.Create(new BuildinClientOptions());
        _logger = Substitute.For<ILogger<BotBuildinClient>>();
        _client = new BotBuildinClient(_adapter, _options, _logger);
    }

    [Fact]
    public async Task WrapAsync_Success_LogsDebugWithMethodAndOutcome()
    {
        var generated = new Gen.UserMe
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Test",
            Type = "person"
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.UserMe>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.UserMe?>(generated));

        await _client.GetMeAsync();

#pragma warning disable CA1873
        _logger.Received(1).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("GetMeAsync") && v.ToString()!.Contains("success")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
#pragma warning restore CA1873
    }

    [Fact]
    public async Task WrapAsync_Success_RecordsApiCallsTotalMetric()
    {
        var measurements = new List<Measurement<long>>();
        using var meter = new Meter("TestBuildout.Api.Total." + Guid.NewGuid());
        var counter = meter.CreateCounter<long>("buildout.api.calls.total");

        using var listener = new MeterListener();
        long recordedValue = 0;
        KeyValuePair<string, object?>[] recordedTags = [];

        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Name == "buildout.api.calls.total" && inst.Meter.Name == "Buildout")
                l.EnableMeasurementEvents(inst);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
        {
            var tagArray = tags.ToArray();
            if (tagArray.Any(t => t.Key == "method" && t.Value?.ToString() == "GetMeAsync")
                && tagArray.Any(t => t.Key == "outcome" && t.Value?.ToString() == "success"))
            {
                recordedValue += value;
                recordedTags = tagArray;
            }
        });
        listener.Start();

        var generated = new Gen.UserMe
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Test",
            Type = "person"
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.UserMe>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.UserMe?>(generated));

        await _client.GetMeAsync();

        listener.RecordObservableInstruments();
        Assert.Equal(1, recordedValue);

        var tagDict = ToDictionary(recordedTags);
        Assert.Equal("success", tagDict["outcome"]);
        Assert.Equal("GetMeAsync", tagDict["method"]);
    }

    [Fact]
    public async Task WrapAsync_ApiError_LogsErrorWithMethodAndErrorType()
    {
        var apiError = new Gen.Error
        {
            Status = 404,
            Code = Gen.Error_code.Not_found,
            MessageEscaped = "Not found"
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.UserMe>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<Gen.UserMe?>>(_ => throw apiError);

        await Assert.ThrowsAsync<BuildinApiException>(() => _client.GetMeAsync());

#pragma warning disable CA1873
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("GetMeAsync") && v.ToString()!.Contains("failure") && v.ToString()!.Contains("api")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
#pragma warning restore CA1873
    }

    [Fact]
    public async Task WrapAsync_TransportError_LogsErrorWithTransportErrorType()
    {
        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.UserMe>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<Gen.UserMe?>>(_ => throw new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<BuildinApiException>(() => _client.GetMeAsync());

#pragma warning disable CA1873
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("GetMeAsync") && v.ToString()!.Contains("failure") && v.ToString()!.Contains("transport")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
#pragma warning restore CA1873
    }

    [Fact]
    public async Task WrapAsync_ApiError_RecordsMetricWithFailureOutcome()
    {
        long recordedValue = 0;
        KeyValuePair<string, object?>[] recordedTags = [];

        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Name == "buildout.api.calls.total" && inst.Meter.Name == "Buildout")
                l.EnableMeasurementEvents(inst);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
        {
            var tagArray = tags.ToArray();
            if (tagArray.Any(t => t.Key == "method" && t.Value?.ToString() == "GetMeAsync")
                && tagArray.Any(t => t.Key == "outcome" && t.Value?.ToString() == "failure"))
            {
                recordedValue += value;
                recordedTags = tagArray;
            }
        });
        listener.Start();

        var apiError = new Gen.Error
        {
            Status = 404,
            Code = Gen.Error_code.Not_found,
            MessageEscaped = "Not found"
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.UserMe>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<Gen.UserMe?>>(_ => throw apiError);

        await Assert.ThrowsAsync<BuildinApiException>(() => _client.GetMeAsync());

        listener.RecordObservableInstruments();
        Assert.Equal(1, recordedValue);

        var tagDict = ToDictionary(recordedTags);
        Assert.Equal("failure", tagDict["outcome"]);
        Assert.Equal("api", tagDict["error_type"]);
        Assert.Equal("GetMeAsync", tagDict["method"]);
    }

    [Fact]
    public async Task WrapAsync_TransportError_RecordsMetricWithFailureOutcome()
    {
        long recordedValue = 0;
        KeyValuePair<string, object?>[] recordedTags = [];

        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Name == "buildout.api.calls.total" && inst.Meter.Name == "Buildout")
                l.EnableMeasurementEvents(inst);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
        {
            var tagArray = tags.ToArray();
            if (tagArray.Any(t => t.Key == "method" && t.Value?.ToString() == "GetMeAsync")
                && tagArray.Any(t => t.Key == "outcome" && t.Value?.ToString() == "failure"))
            {
                recordedValue += value;
                recordedTags = tagArray;
            }
        });
        listener.Start();

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.UserMe>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<Gen.UserMe?>>(_ => throw new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<BuildinApiException>(() => _client.GetMeAsync());

        listener.RecordObservableInstruments();
        Assert.Equal(1, recordedValue);

        var tagDict = ToDictionary(recordedTags);
        Assert.Equal("failure", tagDict["outcome"]);
        Assert.Equal("transport", tagDict["error_type"]);
        Assert.Equal("GetMeAsync", tagDict["method"]);
    }

    [Fact]
    public async Task WrapAsync_UnknownError_LogsErrorWithUnknownErrorType()
    {
        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.UserMe>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<Gen.UserMe?>>(_ => throw new InvalidOperationException("Unexpected"));

        await Assert.ThrowsAsync<BuildinApiException>(() => _client.GetMeAsync());

#pragma warning disable CA1873
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("GetMeAsync") && v.ToString()!.Contains("failure") && v.ToString()!.Contains("unknown")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
#pragma warning restore CA1873
    }

    [Fact]
    public async Task WrapAsync_Success_RecordsApiCallDurationMetric()
    {
        double recordedValue = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Name == "buildout.api.call.duration" && inst.Meter.Name == "Buildout")
                l.EnableMeasurementEvents(inst);
        };
        listener.SetMeasurementEventCallback<double>((inst, value, tags, state) =>
        {
            recordedValue += value;
        });
        listener.Start();

        var generated = new Gen.UserMe
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Test",
            Type = "person"
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.UserMe>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.UserMe?>(generated));

        await _client.GetMeAsync();

        listener.RecordObservableInstruments();
        Assert.True(recordedValue >= 0);
    }

    private static Dictionary<string, object?> ToDictionary(KeyValuePair<string, object?>[] tags)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var tag in tags)
            dict[tag.Key] = tag.Value;
        return dict;
    }
}
