namespace Buildout.Core.Auth;

public interface IRequestAuthenticator
{
    Task<AuthResult> AuthenticateAsync(string? authorizationHeader);
}