namespace Buildout.Core.Buildin.Errors;

public sealed class BuildinApiException : Exception
{
    public BuildinError Error { get; }

    public BuildinApiException(BuildinError error)
        : base(error switch
        {
            TransportError te => te.Cause.Message,
            ApiError ae => ae.Message,
            UnknownError ue => $"Unknown error with status {ue.StatusCode}",
            _ => "An unexpected error occurred."
        })
    {
        Error = error;
    }

    public BuildinApiException(BuildinError error, Exception innerException)
        : base(error switch
        {
            TransportError te => te.Cause.Message,
            ApiError ae => ae.Message,
            UnknownError ue => $"Unknown error with status {ue.StatusCode}",
            _ => "An unexpected error occurred."
        }, innerException)
    {
        Error = error;
    }
}
