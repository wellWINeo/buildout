namespace Buildout.Core.Auth;

public sealed record AuthResult
{
    public bool IsAuthenticated { get; init; }
    public string? ResolvedBotToken { get; init; }
    public string? TokenIdentity { get; init; }
    public string? ErrorMessage { get; init; }

    private AuthResult()
    {
    }

    public static AuthResult Success(string botToken, string? identity)
    {
        return new AuthResult
        {
            IsAuthenticated = true,
            ResolvedBotToken = botToken,
            TokenIdentity = identity,
            ErrorMessage = null
        };
    }

    public static AuthResult Failure(string error)
    {
        return new AuthResult
        {
            IsAuthenticated = false,
            ResolvedBotToken = null,
            TokenIdentity = null,
            ErrorMessage = error
        };
    }
}