namespace Buildout.Core.Buildin.Errors;

public abstract record BuildinError;

public sealed record TransportError(Exception Cause) : BuildinError;

public sealed record ApiError(int StatusCode, string? Code, string Message, string? RawBody) : BuildinError;

public sealed record UnknownError(int StatusCode, string RawBody) : BuildinError;
