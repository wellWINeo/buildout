namespace Buildout.Core.Buildin.Errors;

public abstract record BuildinError;

public sealed record TransportError(Exception Cause) : BuildinError;

public sealed record ApiErrorDetail(
    string? Path,
    string? Reason,
    int? Limit,
    int? Actual,
    IReadOnlyDictionary<string, string>? AdditionalFields = null);

public sealed record ApiError(int StatusCode, string? Code, string Message, string? RawBody) : BuildinError
{
    public string? RequestId { get; init; }
    public IReadOnlyList<ApiErrorDetail>? Details { get; init; }
    public TimeSpan? RetryAfter { get; init; }
    public string Category => StatusCode switch
    {
        400 => "validation", 401 => "authentication", 403 => "authorization", 404 => "not_found",
        409 => "conflict", 429 => "rate_limited", _ => StatusCode >= 500 ? "unexpected" : "api"
    };
}

public sealed record UnknownError(int StatusCode, string RawBody) : BuildinError;
