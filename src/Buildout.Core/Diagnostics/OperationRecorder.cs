using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Buildout.Core.Diagnostics;

public sealed class OperationRecorder : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _operationName;
    private readonly Stopwatch _stopwatch;
    private readonly Dictionary<string, object?> _tags;
    private bool _completed;

    private OperationRecorder(ILogger logger, string operationName, KeyValuePair<string, object?>[]? tags)
    {
        _logger = logger;
        _operationName = operationName;
        _stopwatch = Stopwatch.StartNew();
        _tags = [];

        if (tags is not null)
        {
            foreach (var tag in tags)
                _tags[tag.Key] = tag.Value;
        }

        OperationRecorderLog.OperationStarted(_logger, _operationName);
    }

    public static OperationRecorder Start(ILogger logger, string operationName,
        KeyValuePair<string, object?>[]? tags = null)
    {
        return new OperationRecorder(logger, operationName, tags);
    }

    public void SetTag(string key, object? value)
    {
        _tags[key] = value;
    }

    public void Succeed()
    {
        if (_completed) return;
        _completed = true;
        _stopwatch.Stop();

        var durationMs = _stopwatch.Elapsed.TotalMilliseconds;
        var durationSeconds = _stopwatch.Elapsed.TotalSeconds;

        var tagSuffix = FormatTags();
        OperationRecorderLog.OperationCompleted(_logger, _operationName, durationMs, tagSuffix);

        var tagList = BuildTagList("success");
        BuildoutMeter.OperationsTotal.Add(1, tagList);
        BuildoutMeter.OperationDuration.Record(durationSeconds, tagList);
    }

    public void Fail(string errorType, int? statusCode = null)
    {
        if (_completed) return;
        _completed = true;
        _stopwatch.Stop();

        var durationMs = _stopwatch.Elapsed.TotalMilliseconds;
        var durationSeconds = _stopwatch.Elapsed.TotalSeconds;

        _tags["error_type"] = errorType;
        if (statusCode.HasValue)
            _tags["status_code"] = statusCode.Value;

        var tagSuffix = FormatTags();
        OperationRecorderLog.OperationFailed(_logger, _operationName, errorType, statusCode, durationMs, tagSuffix);

        var tagList = BuildTagList("failure");
        BuildoutMeter.OperationsTotal.Add(1, tagList);
        BuildoutMeter.OperationDuration.Record(durationSeconds, tagList);
    }

    public void Dispose()
    {
        if (!_completed)
            Fail("unknown");
    }

    private string FormatTags()
    {
        if (_tags.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var tag in _tags)
        {
            if (tag.Value is not null)
            {
                sb.Append(' ');
                sb.Append(tag.Key);
                sb.Append('=');
                sb.Append(tag.Value);
            }
        }
        return sb.ToString();
    }

    private TagList BuildTagList(string outcome)
    {
        var tags = new TagList
        {
            { "operation", _operationName },
            { "outcome", outcome }
        };

        foreach (var tag in _tags)
        {
            if (tag.Key is not ("outcome" or "duration_ms"))
                tags.Add(tag.Key, tag.Value?.ToString() ?? string.Empty);
        }

        return tags;
    }
}
