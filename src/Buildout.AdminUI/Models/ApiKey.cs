namespace Buildout.AdminUI.Models;

public sealed class ApiKey
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public ApiKeyStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
}
